﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameBotCore.V1._3;

namespace Elympics
{
	internal class HalfRemoteGameBotInitializer : GameBotInitializer
	{
		private HalfRemoteMatchClientAdapter _halfRemoteMatchClient;
		private HalfRemoteMatchConnectClient _halfRemoteMatchConnectClient;

		protected override void InitializeBot(ElympicsBot bot, ElympicsGameConfig elympicsGameConfig, GameBotAdapter gameBotAdapter)
		{
			var playerIndex = elympicsGameConfig.PlayerIndexForHalfRemoteMode;
			var playersList = DebugPlayerListCreator.CreatePlayersList(elympicsGameConfig);
			var userId = playersList[playerIndex].UserId;

			var botConfiguration = new BotConfiguration
			{
				Difficulty = 0,
				UserId = userId,
				MatchPlayers = playersList.Select(x => x.UserId).ToList(),
				MatchId = null,
				MatchmakerData = playersList[playerIndex].MatchmakerData,
				GameEngineData = playersList[playerIndex].GameEngineData,
			};

			_halfRemoteMatchClient = new HalfRemoteMatchClientAdapter(elympicsGameConfig);
			_halfRemoteMatchConnectClient = new HalfRemoteMatchConnectClient(_halfRemoteMatchClient, elympicsGameConfig.IpForHalfRemoteMode, elympicsGameConfig.TcpPortForHalfRemoteMode, userId, elympicsGameConfig.UseWebInHalfRemote);

			_halfRemoteMatchClient.RawSnapshotReceived += gameBotAdapter.OnInGameDataUnreliableReceived;
			gameBotAdapter.InGameDataForReliableChannelGenerated += data => _halfRemoteMatchClient.SendRawInputReliable(data);
			gameBotAdapter.InGameDataForUnreliableChannelGenerated += data => _halfRemoteMatchClient.SendRawInputUnreliable(data);
			
			gameBotAdapter.Init(new LoggerNoop(), null);
			gameBotAdapter.Init2(null);
			gameBotAdapter.Init3(botConfiguration);
			
			_halfRemoteMatchConnectClient.ConnectAndJoinAsPlayer(_ => {}, default);
		}

		public override void Dispose()
		{
			_halfRemoteMatchConnectClient?.Dispose();
		}
	}
}
