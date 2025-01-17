#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Elympics
{
	public static class BuildTools
	{
		private const string ServerBuildPath    = "serverbuild";
		private const string EngineSubdirectory = "Engine";
		private const string BotSubdirectory    = "Bot";
		private const string UnityBuildPath     = "Unity";

		private const string EngineWrapperFilename = "GameEngine.dll";
		private const string BotWrapperFilename    = "GameBot.dll";

		private const string ServerBuildAppNameLinux   = "Unity";
		private const string ServerBuildAppNameWindows = "Unity.exe";

		private static readonly string GuidOfAssetsPathPointer		= AssetDatabase.FindAssets("t:ElympicsBasePath")[0];
		private static readonly string BuildAssetsPath				= Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(GuidOfAssetsPathPointer));
		private static readonly string ServerWrapperPath			= Path.Combine(BuildAssetsPath, "Wrapper");
		private static readonly string GameBotNoopPath				= Path.Combine(BuildAssetsPath, "GameBotNoop");
		private const           string ServerWrapperFilesPattern    = "*.dll_";
		private const           string ServerWrapperTargetExtension = ".dll";

		internal static string EnginePath => Path.Combine(ServerBuildPath, EngineSubdirectory);
		internal static string BotPath    => Path.Combine(ServerBuildPath, BotSubdirectory);

		private static readonly Regex MissingModuleRegex = new Regex(
			@"build target (was )?unsupported|LinuxStandalone|scripting backend (\(\w+\) )?(is )?not installed",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private const string MissingModuleErrorMessage =
#if UNITY_2021_2_OR_NEWER
			"Installation of Unity modules is required: Linux Build Support (Mono) and Linux Dedicated Server Build Support";
#else
			"Installation of Unity module is required: Linux Build Support (Mono)";
#endif

		public static void UpdateElympicsGameVersion(string newGameVersion)
		{
			var config = ElympicsConfig.LoadCurrentElympicsGameConfig();
			if (config == null)
				throw new Exception("[Elympics] Elympics config not found");

			config.UpdateGameVersion(newGameVersion);
		}

		internal static bool BuildServerWindows()
		{
			return BuildServer(ServerBuildAppNameWindows, BuildTarget.StandaloneWindows64);
		}

		internal static bool BuildServerLinux()
		{
			return BuildServer(ServerBuildAppNameLinux, BuildTarget.StandaloneLinux64);
		}

		private static bool? IsLinuxModuleInstalled()
		{
			var moduleManager = System.Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");
			var isPlatformSupportLoadedByBuildTarget = moduleManager?.GetMethod("IsPlatformSupportLoadedByBuildTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			return (bool?)isPlatformSupportLoadedByBuildTarget?.Invoke(null, new object[] { BuildTarget.StandaloneLinux64 });
		}

		internal static bool BuildElympicsServerLinux()
		{
			if (IsLinuxModuleInstalled() is false)
			{
				Debug.LogError(MissingModuleErrorMessage);
				return false;
			}

			var isBuildSuccess = BuildServerLinux();
			if (isBuildSuccess)
				RemoveHalfRemoteServerFilesFromElympicsBuild();

			return isBuildSuccess;
		}

		private static bool BuildServer(string appName, BuildTarget target)
		{
			try
			{
				var title = $"Building server for {appName}";
				EditorUtility.DisplayProgressBar(title, "Loading elympics game config", 0);
				var config = ElympicsConfig.LoadCurrentElympicsGameConfig();
				if (config == null)
					throw new Exception("[Elympics] Elympics config not found");

				var sceneToBuild = new[] { config.GameplayScene };
				EditorUtility.DisplayProgressBar(title, $"Using scene {config.GameplayScene}", 0.15f);

				var buildTargetGroup = BuildTargetGroup.Standalone;
				var oldScriptingBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup);
				PlayerSettings.SetScriptingBackend(buildTargetGroup, ScriptingImplementation.Mono2x);

				EditorUtility.DisplayProgressBar(title, "Removing old server build path", 0.3f);
				if (Directory.Exists(ServerBuildPath))
					Directory.Delete(ServerBuildPath, true);

				EditorUtility.DisplayProgressBar(title, "Building player", 0.45f);
				var buildPlayerOptions = new BuildPlayerOptions
				{
					scenes = sceneToBuild,
					locationPathName = Path.Combine(ServerBuildPath, EngineSubdirectory, UnityBuildPath, appName),
					targetGroup = buildTargetGroup,
					target = target,
#if UNITY_2021_2_OR_NEWER
					subtarget = (int)StandaloneBuildSubtarget.Server,
					options = BuildOptions.CompressWithLz4HC
#else
					options = BuildOptions.EnableHeadlessMode | BuildOptions.CompressWithLz4HC
#endif
				};

				var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
				LogBuildResult(report);

				// Restore
				PlayerSettings.SetScriptingBackend(buildTargetGroup, oldScriptingBackend);

				if (report.summary.result != BuildResult.Succeeded)
					return false;

				// Copy and pack

				EditorUtility.DisplayProgressBar(title, "Copying engine wrapper to build path", 0.6f);
				CopyWrapperToBuildPath(BotWrapperFilename, EngineSubdirectory);

				EditorUtility.DisplayProgressBar(title, "Copying bot wrapper to build path", 0.75f);
				if (config.BotsInServer)
					CopyGameBotNoopToBuildPath();
				else
					CopyWrapperToBuildPath(EngineWrapperFilename, BotSubdirectory);

				EditorUtility.DisplayProgressBar(title, $"Build finished at {ServerBuildPath}", 1f);

				return true;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static void CopyWrapperToBuildPath(string excludedFilename, string subdirectory)
		{
			Directory.CreateDirectory(Path.Combine(ServerBuildPath, subdirectory));
			var wrapperFiles = Directory.GetFiles(ServerWrapperPath, ServerWrapperFilesPattern);
			wrapperFiles = wrapperFiles.Where(x => !x.Contains(excludedFilename)).ToArray();
			foreach (var wrapperFile in wrapperFiles)
			{
				var filename = Path.GetFileName(wrapperFile);
				var targetFile = Path.Combine(ServerBuildPath, subdirectory, filename);
				targetFile = Path.ChangeExtension(targetFile, ServerWrapperTargetExtension);
				File.Copy(wrapperFile, targetFile);
			}
		}

		private static void CopyGameBotNoopToBuildPath()
		{
			Directory.CreateDirectory(Path.Combine(ServerBuildPath, BotSubdirectory));
			var botFiles = Directory.GetFiles(GameBotNoopPath, ServerWrapperFilesPattern);
			foreach (var botFile in botFiles)
			{
				var filename = Path.GetFileName(botFile);
				var targetFile = Path.Combine(ServerBuildPath, BotSubdirectory, filename);
				targetFile = Path.ChangeExtension(targetFile, ServerWrapperTargetExtension);
				File.Copy(botFile, targetFile);
			}
		}

		private static void LogBuildResult(BuildReport report)
		{
			if (report.summary.result == BuildResult.Succeeded)
				Debug.Log($"Server build succeeded on {report.summary.outputPath}");
			else
			{
				Debug.LogError($"Server build failed with {report.summary.totalErrors} errors");
				ProcessBuildErrors(report);
			}
		}

		private static void ProcessBuildErrors(BuildReport report)
		{
			if (report.summary.totalErrors == 0)
				return;

			foreach (var step in report.steps)
				foreach (var message in step.messages)
				{
					if (message.type != LogType.Error && message.type != LogType.Exception)
						continue;
					if (MissingModuleRegex.IsMatch(message.content))
					{
						Debug.LogError(MissingModuleErrorMessage);
						return;
					}
				}
		}

		// Required - linux server not working in headless mode with WebRTC library ~pprzestrzelski 08.11.2021
		private static void RemoveHalfRemoteServerFilesFromElympicsBuild()
		{
			var pluginsPath = Path.Combine(ServerBuildPath, EngineSubdirectory, ServerBuildAppNameLinux, $"{ServerBuildAppNameLinux}_Data", "Plugins");

			var webRtcLibPath = Path.Combine(pluginsPath, "webrtc.so");
			if (Directory.Exists(pluginsPath))
				File.Delete(webRtcLibPath);
		}
	}
}
#endif
