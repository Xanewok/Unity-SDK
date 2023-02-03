using System.Collections.Generic;

namespace Elympics
{
	public struct ElympicsBehaviourMetadata
	{
		public string                     Name;
		public int                        NetworkId;
		public ElympicsPlayer             PredictableFor;
		public string                     PrefabName;
		public Dictionary<string, string> StateMetadata;
	}
}
