using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ElympicsApiModels.ApiModels.Games;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace Elympics
{
	public static class ElympicsWebClient
	{

		private static string sdkVersion = ElympicsConfig.Load().ElympicsVersion;
#if UNITY_EDITOR
		public static UnityWebRequestAsyncOperation SendEnginePostRequestApi(string url, string gameVersion, string[] filesPath, Action<UnityWebRequest> completed = null)
		{
			var formData = new List<IMultipartFormSection>();
			foreach (var path in filesPath)
				formData.Add(new MultipartFormFileSection(Routes.GamesUploadRequestFilesFieldName, File.ReadAllBytes(path), "pack.zip", "multipart/form-data"));
			formData.Add(new MultipartFormDataSection("gameVersion", gameVersion));
			var uri = new Uri(url);
			var request = UnityWebRequest.Post(uri, formData);
			request.SetRequestHeader("Authorization", $"Bearer {ElympicsConfig.AuthToken}");
			request.SetRequestHeader("elympics-sdk-version", sdkVersion);

			AcceptTestCertificateHandler.SetOnRequestIfNeeded(request);

			var asyncOperation = request.SendWebRequest();
			AttachCompletedCallback(asyncOperation, completed);
			return asyncOperation;
		}

		private static void AttachCompletedCallback(UnityWebRequestAsyncOperation asyncOperation, Action<UnityWebRequest> completed = null)
		{
			if (completed == null)
				return;
			if (asyncOperation.isDone)
				completed.Invoke(asyncOperation.webRequest);
			else
				asyncOperation.completed += _ => completed?.Invoke(asyncOperation.webRequest);
		}

		public static UnityWebRequestAsyncOperation SendJsonGetRequestApi(string url, Action<UnityWebRequest> completed = null)
		{
			var uri = new Uri(url);
			var request = UnityWebRequest.Get(uri);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Authorization", $"Bearer {ElympicsConfig.AuthToken}");
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Accept", "application/json");
			request.SetRequestHeader("elympics-sdk-version", sdkVersion);

			AcceptTestCertificateHandler.SetOnRequestIfNeeded(request);

			Debug.Log($"[Elympics] Sending request GET {url}");

			var asyncOperation = request.SendWebRequest();
			AttachCompletedCallback(asyncOperation, completed);
			return asyncOperation;
		}

		public static UnityWebRequestAsyncOperation SendJsonPostRequestApi(string url, object body, Action<UnityWebRequest> completed = null, bool auth = true)
		{
			var asyncOperation = SendJsonPostRequest(url, body, auth ? $"Bearer {ElympicsConfig.AuthToken}" : null);
			AttachCompletedCallback(asyncOperation, completed);
			return asyncOperation;
		}
#endif

		public static UnityWebRequestAsyncOperation SendJsonPostRequest(string url, object body, string auth)
		{
			var uri = new Uri(url);
			var request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);
			var bodyString = JsonUtility.ToJson(body);
			var bodyRaw = Encoding.ASCII.GetBytes(bodyString);
			request.uploadHandler = new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = new DownloadHandlerBuffer();

			if (!string.IsNullOrEmpty(auth))
				request.SetRequestHeader("Authorization", auth);
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Accept", "application/json");
			request.SetRequestHeader("elympics-sdk-version", sdkVersion);

			AcceptTestCertificateHandler.SetOnRequestIfNeeded(request);

			Debug.Log($"[Elympics] Sending request POST {url}\n{bodyString}");
			return request.SendWebRequest();
		}

		public static bool TryDeserializeResponse<T>(UnityWebRequest webRequest, string actionName, out T deserializedResponse)
		{
			deserializedResponse = default;
			if (webRequest.isHttpError || webRequest.isNetworkError)
			{
				var errorMassage = ParseResponseErrors(webRequest);
				Debug.LogError($"Error occuert on {actionName}: {errorMassage}");
				return false;
			}

			var jsonBody = webRequest.downloadHandler.text;
			var type = typeof(T);
			if (type == typeof(string))
			{
				deserializedResponse = (T)(object)jsonBody;
			}
			else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				DeserializeToList(out deserializedResponse, jsonBody);
			}
			else
			{
				deserializedResponse = JsonUtility.FromJson<T>(jsonBody);
			}

			return true;
		}

		private static void DeserializeToList<T>(out T deserializedResponse, string jsonBody)
		{
			var replacedJsonForWrapper = "";
			if (jsonBody.Contains("$values"))
			{
				replacedJsonForWrapper = jsonBody.Replace("$values", "List");
			}
			else if (jsonBody.StartsWith("["))
			{
				replacedJsonForWrapper = $"{{ \"List\": {jsonBody} }}";
			}

			var deserializedWrapper = JsonUtility.FromJson<ListWrapper<T>>(replacedJsonForWrapper);
			deserializedResponse = deserializedWrapper.List;
		}

		public static string ParseResponseErrors(UnityWebRequest request)
		{
			string errorMassage;

			if (request.responseCode == 403)
				errorMassage = "Requested resource is forbidden for this account";

			if (request.responseCode == 401)
				errorMassage = "Unauthorized, please login to your ElympicsWeb account";

			if (request.isNetworkError)
				errorMassage = $"Network error - {request.error}";

			ErrorModel errorModel = null;
			try
			{
				errorModel = JsonConvert.DeserializeObject<ErrorModel>(request.downloadHandler.text);
			}
			catch (ArgumentException)
			{
			}

			if (errorModel?.Errors == null)
			{
				errorMassage = $"Received error response code {request.responseCode} with error\n{request.downloadHandler.text}";
			}
			else
			{
				var errors = string.Join(", ", errorModel.Errors.SelectMany(r => r.Value.Select(x => x)));
				errorMassage = $"Received error response code {request.responseCode} with errors\n{errors}";
			}

			Debug.LogError(errorMassage);
			return (errorMassage);
		}

		[Serializable]
		public class ListWrapper<T>
		{
			public T List;
		}

		[Serializable]
		private class ErrorModel
		{
			public Dictionary<string, string[]> Errors = null;
		}

		public class AcceptTestCertificateHandler : CertificateHandler
		{
			private const string TestDomain = ".test";
			private const string VagrantTestHostPart = "vagrant";

			public static void SetOnRequestIfNeeded(UnityWebRequest request)
			{
				if (!request.uri.Host.Contains(TestDomain) && !request.uri.Host.Contains(VagrantTestHostPart))
					return;

				Debug.Log($"Test domain cert handler set for domain {request.uri.Host}");
				request.certificateHandler = new AcceptTestCertificateHandler();
			}

			protected override bool ValidateCertificate(byte[] certificateData)
			{
				var certificate = new X509Certificate2(certificateData);
				return certificate.Subject.ToLower().Contains(TestDomain);
			}
		}
	}
}
