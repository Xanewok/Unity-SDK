using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elympics
{
	[CustomEditor(typeof(ElympicsGameConfig))]
	internal partial class ElympicsGameConfigEditor : Editor
	{
		private const int MaxTicks         = 128;
		private const int MinTicks         = 1;
		private const int MaxLagInMs       = 2000;
		private const int MinInputLagTicks = 1;
		private const int MaxInputLagMs    = 500;
		private const int UnitLabelWidth   = 30;

		private ElympicsGameConfig _config;

		private SerializedProperty _gameName;
		private SerializedProperty _gameId;
		private SerializedProperty _gameVersion;
		private SerializedProperty _players;
		private SerializedProperty _gameplaySceneAsset;
		private SerializedProperty _gameplayScene;

		private SerializedProperty _botsInServer;

		private SerializedProperty _useWeb;
		private SerializedProperty _enableReconnect;
		private SerializedProperty _ticksPerSecond;
		private SerializedProperty _snapshotSendingPeriodInTicks;
		private SerializedProperty _inputLagTicks;
		private SerializedProperty _maxAllowedLagInTicks;
		private SerializedProperty _prediction;
		private SerializedProperty _predictionLimitInTicks;

		private SerializedProperty _detailedNetworkLog;
		private SerializedProperty _mode;
		private SerializedProperty _halfRemoteMode;
		private SerializedProperty _useWebInHalfRemote;
		private SerializedProperty _ipForHalfRemoteMode;
		private SerializedProperty _tcpPortForHalfRemoteMode;
		private SerializedProperty _webPortForHalfRemoteMode;
		private SerializedProperty _playerIndexForHalfRemoteMode;
		private SerializedProperty _testMatchDataQueue;
		private SerializedProperty _testMatchDataRegion;
		private SerializedProperty _testPlayers;

		private GUIStyle     _indentation;
		private Stack<float> _labelWidthStack;

		private bool _verifyGameScenePath;
		private bool _showGameSetup;
		private bool _currentGameVersionIsUploadedStatusUnknown = true;

		private void OnEnable()
		{
			try
			{
				_config = serializedObject.targetObject as ElympicsGameConfig;

				CurrentGameVersionUploadedToTheCloudStatus.CheckingIfGameVersionIsUploadedChanged += (isCheckingGameVersions) => _currentGameVersionIsUploadedStatusUnknown = isCheckingGameVersions;
				CurrentGameVersionUploadedToTheCloudStatus.Initialize(_config);
			}
			catch (Exception)
			{
				return;
			}

			_gameName = serializedObject.FindProperty("gameName");
			_gameId = serializedObject.FindProperty("gameId");
			_gameVersion = serializedObject.FindProperty("gameVersion");
			_players = serializedObject.FindProperty("players");
			_gameplaySceneAsset = serializedObject.FindProperty("gameplaySceneAsset");
			_gameplayScene = serializedObject.FindProperty("gameplayScene");

			_botsInServer = serializedObject.FindProperty("botsInServer");

			_useWeb = serializedObject.FindProperty("useWeb");
			_enableReconnect = serializedObject.FindProperty("enableReconnect");
			_ticksPerSecond = serializedObject.FindProperty("ticksPerSecond");
			_snapshotSendingPeriodInTicks = serializedObject.FindProperty("snapshotSendingPeriodInTicks");
			_inputLagTicks = serializedObject.FindProperty("inputLagTicks");
			_maxAllowedLagInTicks = serializedObject.FindProperty("maxAllowedLagInTicks");
			_prediction = serializedObject.FindProperty("prediction");
			_predictionLimitInTicks = serializedObject.FindProperty("predictionLimitInTicks");

			_detailedNetworkLog = serializedObject.FindProperty("detailedNetworkLog");
			_mode = serializedObject.FindProperty("mode");
			_halfRemoteMode = serializedObject.FindProperty("halfRemoteMode");
			_useWebInHalfRemote = serializedObject.FindProperty("useWebInHalfRemote");
			_ipForHalfRemoteMode = serializedObject.FindProperty("ipForHalfRemoteMode");
			_tcpPortForHalfRemoteMode = serializedObject.FindProperty("tcpPortForHalfRemoteMode");
			_webPortForHalfRemoteMode = serializedObject.FindProperty("webPortForHalfRemoteMode");
			_playerIndexForHalfRemoteMode = serializedObject.FindProperty("playerIndexForHalfRemoteMode");
			var testMatchData = serializedObject.FindProperty("testMatchData");
			_testMatchDataQueue = testMatchData.FindPropertyRelative(nameof(ElympicsGameConfig.InitialMatchData.queueName));
			_testMatchDataRegion = testMatchData.FindPropertyRelative(nameof(ElympicsGameConfig.InitialMatchData.regionName));
			_testPlayers = serializedObject.FindProperty("testPlayers");

			_indentation = new GUIStyle { margin = new RectOffset(10, 0, 0, 0) };
			_labelWidthStack = new Stack<float>();

			_verifyGameScenePath = true;
		}

		private void OnDestroy()
		{
			CurrentGameVersionUploadedToTheCloudStatus.Disable();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var summaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic, wordWrap = true };
			var cachedWordWrap = EditorStyles.label.wordWrap;
			EditorStyles.label.wordWrap = true;

			EditorGUI.BeginDisabledGroup(true);
			_showGameSetup = EditorGUILayout.Foldout(_showGameSetup, "Game Setup");
			EditorGUI.EndDisabledGroup();

			if (_showGameSetup)
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.PropertyField(_gameName, new GUIContent("Game name"));
				EditorGUILayout.PropertyField(_gameId, new GUIContent("Game id"));
				EditorGUILayout.PropertyField(_gameVersion, new GUIContent("Game version"));
				EditorGUILayout.PropertyField(_players, new GUIContent("Players number"));
				serializedObject.ApplyModifiedProperties();
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.Separator();
			}

			DrawGameplayScene();

			BeginSection("Server");
			EditorGUILayout.PropertyField(_botsInServer, new GUIContent("Bots inside server"));
			EditorGUILayout.LabelField(Label_BotsInsideServerSummary, summaryLabelStyle);
			serializedObject.ApplyModifiedProperties();
			EndSection();

			BeginSection("Client");
			DrawUseWeb(summaryLabelStyle);
			EditorGUILayout.PropertyField(_enableReconnect, new GUIContent("Reconnect to match"));
			_ticksPerSecond.intValue = TickSlider("Ticks per second", _ticksPerSecond.intValue, MinTicks, MaxTicks);
			_snapshotSendingPeriodInTicks.intValue = TickSlider("Send snapshot every", _snapshotSendingPeriodInTicks.intValue, MinTicks, _ticksPerSecond.intValue);
			_inputLagTicks.intValue = TickSliderConvertedToMs("Input lag", _inputLagTicks.intValue, MinInputLagTicks, MsToTicks(MaxInputLagMs));
			_maxAllowedLagInTicks.intValue = Math.Max(TickSliderConvertedToMs("Max allowed lag", _maxAllowedLagInTicks.intValue, 0, MsToTicks(MaxLagInMs)), 0);
			EditorGUILayout.PropertyField(_prediction, new GUIContent("Prediction"));
			_predictionLimitInTicks.intValue = TickSliderConvertedToMs("Prediction limit", _predictionLimitInTicks.intValue, 0, _maxAllowedLagInTicks.intValue);
			serializedObject.ApplyModifiedProperties();
			EditorGUILayout.LabelField(new GUIContent("Total prediction limit", "With input lag and snapshot sending period included"), new GUIContent($"{TicksToMs(_config.TotalPredictionLimitInTicks)} ms"), new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight });
			EndSection();
			EditorGUILayout.Separator();

			BeginSection("Development");
			EditorGUILayout.PropertyField(_detailedNetworkLog, new GUIContent("Detailed network log", "The log contains errors and warnings to differentiate between slight and serious throttle"));

			var previousMode = _mode.enumValueIndex;
			EditorGUILayout.PropertyField(_mode, new GUIContent("Mode"));
			if (previousMode != _mode.enumValueIndex)
			{
				if ((ElympicsGameConfig.GameplaySceneDebugModeEnum)_mode.enumValueIndex == ElympicsGameConfig.GameplaySceneDebugModeEnum.HalfRemote && !Application.runInBackground)
					Debug.LogError(SdkLogMessages.Error_HalfRemoteWoRunInBacktround);
			}

			switch ((ElympicsGameConfig.GameplaySceneDebugModeEnum)_mode.enumValueIndex)
			{
				case ElympicsGameConfig.GameplaySceneDebugModeEnum.LocalPlayerAndBots:
					EditorGUILayout.LabelField("Run the server, a single player and bots locally with no networking. Good for anything outside of gameplay, such as UI, graphics and sound design.", summaryLabelStyle);
					EditorGUILayout.HelpBox("This mode is not fit for gameplay development!", MessageType.Warning, true);
					break;
				case ElympicsGameConfig.GameplaySceneDebugModeEnum.HalfRemote:
					EditorGUILayout.LabelField("Run the server, players and bots separately with simulated networking. The mock network can simulate many connection types. Best for gameplay development, provides a semi-realistic game behavior with relatively quick testing cycles. You can also test on multiple devices by providing a non-local server address. A single Unity instance can host either a server, user or bot, use ParrelSync to create more instances.", summaryLabelStyle);
					EditorGUILayout.HelpBox("This mode is only a simulation of production environment!", MessageType.Warning, true);
					DrawHalfRemote();
					break;
				case ElympicsGameConfig.GameplaySceneDebugModeEnum.DebugOnlinePlayer:
					EditorGUILayout.LabelField("Connect as a player to production server (which has to be uploaded beforehand). Realistic environment, occasionally better stack trace. Great for finalizing a feature or release.", summaryLabelStyle);
					DisplayGameVersionInfo();
					DrawInitialMatchData();
					break;
			}

			DrawInitialUserDatas();
			serializedObject.ApplyModifiedProperties();
			EndSection();

			EditorStyles.label.wordWrap = cachedWordWrap;
		}

		private void DisplayGameVersionInfo()
		{
			if (_currentGameVersionIsUploadedStatusUnknown)
				EditorGUILayout.HelpBox($"Checking if current game version is uploaded to the Elympics cloud...", MessageType.Info, true);
			else if (!CurrentGameVersionUploadedToTheCloudStatus.IsVersionUploaded)
				EditorGUILayout.HelpBox($"Current game version is not uploaded to the Elympics cloud! Upload your game first in \"{ElympicsEditorMenuPaths.MANAGE_GAMES_IN_ELYMPICS}\" games in Elympics window!", MessageType.Error, true);
		}

		private void BeginSection(string header)
		{
			EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(_indentation);
			_labelWidthStack.Push(EditorGUIUtility.labelWidth);
			EditorGUIUtility.labelWidth -= _indentation.margin.left;
		}

		private void EndSection()
		{
			EditorGUIUtility.labelWidth = _labelWidthStack.Pop();
			EditorGUILayout.EndVertical();
		}

		private void DrawGameplayScene()
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(_gameplaySceneAsset, new GUIContent("Gameplay scene"));
			if (EditorGUI.EndChangeCheck() || _verifyGameScenePath)
				CheckGameplaySceneAndUpdatePath();
			EditorGUI.EndDisabledGroup();

			if (GUILayout.Button("Open scene"))
				EditorSceneManager.OpenScene(_gameplayScene.stringValue);
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.PropertyField(_gameplayScene, new GUIContent("Gameplay scene path"));
			EditorGUI.EndDisabledGroup();
		}

		private void CheckGameplaySceneAndUpdatePath()
		{
			_verifyGameScenePath = false;

			var previousValue = _gameplaySceneAsset.GetValue();
			serializedObject.ApplyModifiedProperties();

			var newValue = _gameplaySceneAsset.GetValue();
			if (!(newValue is SceneAsset scene))
			{
				_gameplaySceneAsset.SetValue(previousValue);
				serializedObject.ApplyModifiedProperties();
				return;
			}

			var scenePath = AssetDatabase.GetAssetOrScenePath(scene);
			_gameplayScene.stringValue = scenePath;
		}

		private void DrawUseWeb(GUIStyle summaryLabelStyle)
		{
			if (ElympicsGameConfig.IsOverridenByWebGL())
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.Toggle("Use HTTPS/WebRTC (forced by WebGL)", ElympicsGameConfig.GetUseWeb(_useWeb.boolValue));
				EditorGUI.EndDisabledGroup();
			}
			else
			{
				EditorGUILayout.PropertyField(_useWeb, new GUIContent("Use HTTPS/WebRTC"));
				if (_useWeb.boolValue)
					EditorGUILayout.LabelField(Label_WebClientWarning, summaryLabelStyle);
			}
		}

		private void DrawHalfRemote()
		{
			if (ElympicsGameConfig.IsOverridenInHalfRemoteByClone())
				EditorGUI.BeginDisabledGroup(true);

			const string remoteModeDescription = "Half remote mode";

			ElympicsGameConfig.HalfRemoteModeEnum halfRemoteMode;
			if (ElympicsGameConfig.IsOverridenInHalfRemoteByClone())
			{
				halfRemoteMode = (ElympicsGameConfig.HalfRemoteModeEnum)_halfRemoteMode.enumValueIndex;
				halfRemoteMode = ElympicsGameConfig.GetHalfRemoteMode(halfRemoteMode);
				EditorGUILayout.EnumPopup(remoteModeDescription, halfRemoteMode);
			}
			else
			{
				EditorGUILayout.PropertyField(_halfRemoteMode, new GUIContent(remoteModeDescription));
				halfRemoteMode = (ElympicsGameConfig.HalfRemoteModeEnum)_halfRemoteMode.enumValueIndex;
			}

			switch (halfRemoteMode)
			{
				case ElympicsGameConfig.HalfRemoteModeEnum.Server:
					EditorGUILayout.PropertyField(_ipForHalfRemoteMode, new GUIContent("IP Address of server"));
					EditorGUILayout.PropertyField(_tcpPortForHalfRemoteMode, new GUIContent("Port TCP server listens on"));
					EditorGUILayout.PropertyField(_webPortForHalfRemoteMode, new GUIContent("Port Web server listens on"));
					break;
				case ElympicsGameConfig.HalfRemoteModeEnum.Client:
				case ElympicsGameConfig.HalfRemoteModeEnum.Bot:
					if (ElympicsGameConfig.IsOverridenInHalfRemoteByClone())
						EditorGUI.EndDisabledGroup();

					DrawUseWebInHalfRemoteClient();

					EditorGUILayout.PropertyField(_ipForHalfRemoteMode, new GUIContent("IP Address of server"));
					if (ElympicsGameConfig.GetUseWebInHalfRemote(_useWebInHalfRemote.boolValue))
						EditorGUILayout.PropertyField(_webPortForHalfRemoteMode, new GUIContent("Web port of server"));
					else
						EditorGUILayout.PropertyField(_tcpPortForHalfRemoteMode, new GUIContent("TCP port of server"));

					if (ElympicsGameConfig.IsOverridenInHalfRemoteByClone())
						EditorGUI.BeginDisabledGroup(true);

					if (ElympicsGameConfig.IsOverridenInHalfRemoteByClone())
						EditorGUILayout.IntField("Used player index", ElympicsGameConfig.GetHalfRemotePlayerIndex(_playerIndexForHalfRemoteMode.intValue));
					else
						EditorGUILayout.PropertyField(_playerIndexForHalfRemoteMode, new GUIContent("Used player index"));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (ElympicsGameConfig.IsOverridenInHalfRemoteByClone())
				EditorGUI.EndDisabledGroup();

			if (halfRemoteMode == ElympicsGameConfig.HalfRemoteModeEnum.Client)
			{
				EditorGUILayout.LabelField("Lag");
				EditorGUILayout.BeginVertical(_indentation);
				_config.HalfRemoteLagConfig.DelayMs = EditorGUILayout.IntField("Delay milliseconds", _config.HalfRemoteLagConfig.DelayMs);
				_config.HalfRemoteLagConfig.PacketLoss = EditorGUILayout.FloatField("Packet loss", _config.HalfRemoteLagConfig.PacketLoss);
				_config.HalfRemoteLagConfig.JitterMs = EditorGUILayout.IntField("Jitter milliseconds (gaussian)", _config.HalfRemoteLagConfig.JitterMs);
				_config.HalfRemoteLagConfig.RandomSeed = EditorGUILayout.IntField("Random seed", _config.HalfRemoteLagConfig.RandomSeed);
				EditorGUILayout.LabelField("Preset");
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("LAN")) _config.HalfRemoteLagConfig.LoadLan();
				if (GUILayout.Button("Broadband")) _config.HalfRemoteLagConfig.LoadBroadband();
				if (GUILayout.Button("Slow broadband")) _config.HalfRemoteLagConfig.LoadSlowBroadband();
				if (GUILayout.Button("LTE")) _config.HalfRemoteLagConfig.LoadLTE();
				if (GUILayout.Button("3G")) _config.HalfRemoteLagConfig.Load3G();
				if (GUILayout.Button("Total mess")) _config.HalfRemoteLagConfig.LoadTotalMess();
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				EditorGUILayout.Separator();

				_config.ReconciliationFrequency = (ElympicsGameConfig.ReconciliationFrequencyEnum)EditorGUILayout.EnumPopup("Reconcile", _config.ReconciliationFrequency);
			}

			EditorGUILayout.Separator();
		}

		private void DrawUseWebInHalfRemoteClient()
		{
			if (ElympicsGameConfig.IsOverridenByWebGL())
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.Toggle("Use HTTPS/WebRTC (forced by WebGL)", ElympicsGameConfig.GetUseWebInHalfRemote(_useWebInHalfRemote.boolValue));
				EditorGUI.EndDisabledGroup();
			}
			else
				EditorGUILayout.PropertyField(_useWebInHalfRemote, new GUIContent("Use HTTPS/WebRTC"));
		}

		private void DrawInitialMatchData()
		{
			EditorGUILayout.LabelField("Test match data", new GUIStyle(GUI.skin.label) {fontStyle = FontStyle.Italic});
			EditorGUILayout.PropertyField(_testMatchDataQueue);
			EditorGUILayout.PropertyField(_testMatchDataRegion);
			EditorGUILayout.Separator();
		}

		private void DrawInitialUserDatas()
		{
			EditorGUILayout.LabelField("Test players", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic });
			if (!_testPlayers.isArray)
				_testPlayers.SetValue(new List<ElympicsGameConfig.InitialUserData>());

			var wantedArraySize = Mathf.Clamp(_players.intValue, 1, 32);
			while (_testPlayers.arraySize < wantedArraySize)
				_testPlayers.InsertArrayElementAtIndex(_testPlayers.arraySize);

			while (_testPlayers.arraySize > wantedArraySize)
				_testPlayers.DeleteArrayElementAtIndex(_testPlayers.arraySize - 1);

			for (var i = 0; i < _testPlayers.arraySize; i++)
			{
				EditorGUILayout.PropertyField(_testPlayers.GetArrayElementAtIndex(i));
				EditorGUILayout.Separator();
			}
		}

		private int TickSliderConvertedToMs(string label, int value, int left, int right) => TickSliderConvertedToMs(new GUIContent(label), value, left, right);

		private int TickSliderConvertedToMs(GUIContent content, int value, int left, int right)
		{
			var newMsValue = IntSliderWithUnit(content, TicksToMs(value), TicksToMs(left), TicksToMs(right), "ms");
			return Mathf.Clamp(MsToTicks(newMsValue), left, right);
		}

		private int TickSlider(string label, int value, int left, int right) => TickSlider(new GUIContent(label), value, left, right);

		private int TickSlider(GUIContent content, int value, int left, int right)
		{
			return IntSliderWithUnit(content, value, left, right, "ticks");
		}

		private int IntSliderWithUnit(GUIContent content, int value, int left, int right, string unit)
		{
			EditorGUILayout.BeginHorizontal();
			var newValue = EditorGUILayout.IntSlider(content, value, left, right, GUILayout.MaxWidth(float.MaxValue));
			EditorGUILayout.LabelField(unit, GUILayout.Width(UnitLabelWidth));
			EditorGUILayout.EndHorizontal();
			return newValue;
		}

		private int TicksToMs(int ticks)
		{
			return (int)Math.Round(ticks * 1000.0 / _ticksPerSecond.intValue);
		}

		private int MsToTicks(int milliseconds)
		{
			return (int)Math.Round(_ticksPerSecond.intValue * milliseconds / 1000.0);
		}

		[CustomPropertyDrawer(typeof(ElympicsGameConfig.InitialUserData))]
		public class InitialUserDataPropertyDrawer : PropertyDrawer
		{
			private static readonly string[] GameEngineDataShowTypes = { "Base64", "String" };

			private readonly Dictionary<int, bool> _showPlayers            = new Dictionary<int, bool>();
			private readonly Dictionary<int, int>  _gameEngineDataShowType = new Dictionary<int, int>();

			private static readonly Regex NumberRegex = new Regex(@"\d+");

			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				var index = int.Parse(NumberRegex.Match(property.propertyPath).Value, NumberFormatInfo.InvariantInfo);
				if (!_showPlayers.TryGetValue(index, out var showPlayer))
					_showPlayers.Add(index, default);

				showPlayer = EditorGUI.Foldout(position, showPlayer, $"Player {index}", new GUIStyle(EditorStyles.foldout) { margin = new RectOffset(10, 0, 0, 0) });
				_showPlayers[index] = showPlayer;
				if (!showPlayer)
					return;

				EditorGUI.indentLevel += 1;
				var userId = property.FindPropertyRelative("userId");
				var isBot = property.FindPropertyRelative("isBot");
				var botDifficulty = property.FindPropertyRelative("botDifficulty");
				var gameEngineData = property.FindPropertyRelative("gameEngineData");
				var matchmakerData = property.FindPropertyRelative("matchmakerData");

				userId.stringValue = index.ToString();

				EditorGUILayout.PropertyField(isBot);
				if (isBot.boolValue)
					EditorGUILayout.PropertyField(botDifficulty);

				DrawGameEngineData(gameEngineData, index);
				DrawMatchmakerData(matchmakerData);

				EditorGUI.indentLevel -= 1;
			}

			private void DrawGameEngineData(SerializedProperty gameEngineData, int index)
			{
				if (!_gameEngineDataShowType.TryGetValue(index, out var gameEngineDataShowTypeChosen))
					_gameEngineDataShowType.Add(index, default);

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Game engine data");
				EditorGUI.BeginChangeCheck();
				gameEngineDataShowTypeChosen = GUILayout.Toolbar(gameEngineDataShowTypeChosen, GameEngineDataShowTypes, GUI.skin.button, GUI.ToolbarButtonSize.FitToContents);
				if (EditorGUI.EndChangeCheck())
				{
					_gameEngineDataShowType[index] = gameEngineDataShowTypeChosen;
					GUI.FocusControl(null);
				}

				EditorGUILayout.EndHorizontal();

				try
				{
					var gameEngineDataValue = (byte[])gameEngineData.GetValue();
					byte[] newGameEngineDataValue = null;
					switch (GameEngineDataShowTypes[gameEngineDataShowTypeChosen])
					{
						case "Base64":
						{
							var gameEngineDataAsByte64 = Convert.ToBase64String(gameEngineDataValue);
							var newGameEngineDataAsByte64 = EditorGUILayout.TextArea(gameEngineDataAsByte64, new GUIStyle(GUI.skin.textArea) { fixedHeight = 0, stretchWidth = true, stretchHeight = true });
							newGameEngineDataValue = newGameEngineDataAsByte64.Length != 0 ? Convert.FromBase64String(newGameEngineDataAsByte64) : new byte[0];
							break;
						}
						case "String":
						{
							var gameEngineDataAsString = Encoding.ASCII.GetString(gameEngineDataValue);
							var newGameEngineDataAsString = EditorGUILayout.TextArea(gameEngineDataAsString, new GUIStyle(GUI.skin.textArea) { fixedHeight = 0, stretchWidth = true, stretchHeight = true });
							newGameEngineDataValue = newGameEngineDataAsString.Length != 0 ? Encoding.ASCII.GetBytes(newGameEngineDataAsString) : new byte[0];
							break;
						}
					}

					gameEngineData.SetValue(newGameEngineDataValue);
				}
				catch (FormatException e)
				{
					Debug.LogWarning(e.Message);
				}
			}

			private static void DrawMatchmakerData(SerializedProperty matchmakerData)
			{
				var mmDataValue = (float[])matchmakerData.GetValue();
				EditorGUI.BeginChangeCheck();
				var newMmDataValueString = EditorGUILayout.TextField("Matchmaker data", string.Join(" ", mmDataValue.Select(x => x.ToString(CultureInfo.InvariantCulture))));
				if (EditorGUI.EndChangeCheck())
				{
					var newMmDataValue = newMmDataValueString.Length != 0 ? newMmDataValueString.Split(' ').Select(x => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray() : new float[0];
					matchmakerData.SetValue(newMmDataValue);
				}
			}
		}
	}
}