using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Elympics
{
	public class ElympicsServer : ElympicsBase
	{
		private static readonly ElympicsPlayer ServerPlayer = ElympicsPlayer.World;

		private ElympicsPlayer _currentPlayer = ElympicsPlayer.Invalid;
		private bool           _currentIsBot;
		private bool           _currentIsClient;

		public override ElympicsPlayer Player   => _currentPlayer;
		public override bool           IsServer => true;
		public override bool           IsBot    => _currentIsBot;
		public override bool           IsClient => _currentIsClient;

		private bool _handlingBotsOverride;
		private bool _handlingClientsOverride;
		private bool HandlingBotsInServer    => _handlingBotsOverride || Config.BotsInServer;
		private bool HandlingClientsInServer => _handlingClientsOverride;

		private GameEngineAdapter _gameEngineAdapter;
		private ElympicsPlayer[]  _playersOfBots;
		private ElympicsPlayer[]  _playersOfClients;

		private bool _initialized;
		private int  _tick;

		private List<ElympicsInput> _inputList;

		internal void InitializeInternal(ElympicsGameConfig elympicsGameConfig, GameEngineAdapter gameEngineAdapter, bool handlingBotsOverride = false, bool handlingClientsOverride = false)
		{
			SwitchBehaviourToServer();
			_handlingBotsOverride = handlingBotsOverride;
			_handlingClientsOverride = handlingClientsOverride;
			base.InitializeInternal(elympicsGameConfig);
			_gameEngineAdapter = gameEngineAdapter;
			_tick = 0;
			_inputList = new List<ElympicsInput>();
			elympicsBehavioursManager.InitializeInternal(this);
			SetupCallbacks();
		}

		private void SetupCallbacks()
		{
			_gameEngineAdapter.PlayerConnected += OnPlayerConnected;
			_gameEngineAdapter.PlayerDisconnected += OnPlayerDisconnected;
			_gameEngineAdapter.InitializedWithMatchPlayerDatas += OnServerInit;
			_gameEngineAdapter.InitializedWithMatchPlayerDatas += InitializeBotsAndClientInServer;
			_gameEngineAdapter.InitializedWithMatchPlayerDatas += SetServerInitializedWithinAsyncQueue;
		}

		private void SetServerInitializedWithinAsyncQueue(InitialMatchPlayerDatas _) => Enqueue(() => _initialized = true);

		private void InitializeBotsAndClientInServer(InitialMatchPlayerDatas data)
		{
			if (HandlingBotsInServer)
			{
				var dataOfBots = data.Where(x => x.IsBot).ToList();
				OnBotsOnServerInit(new InitialMatchPlayerDatas(dataOfBots));

				_playersOfBots = dataOfBots.Select(x => x.Player).ToArray();
				CallPlayerConnectedFromBotsOrClients(_playersOfBots);
			}

			if (HandlingClientsInServer)
			{
				var dataOfClients = data.Where(x => !x.IsBot).ToList();
				OnClientsOnServerInit(new InitialMatchPlayerDatas(dataOfClients));

				_playersOfClients = dataOfClients.Select(x => x.Player).ToArray();
				CallPlayerConnectedFromBotsOrClients(_playersOfClients);
			}
		}


		private void CallPlayerConnectedFromBotsOrClients(ElympicsPlayer[] players)
		{
			foreach (var player in players)
				OnPlayerConnected(player);
		}

		protected override bool ShouldDoFixedUpdate() => _initialized;

		protected override void DoFixedUpdate()
		{
			if (HandlingBotsInServer)
				GatherInputsFromServerBotsOrClient(_playersOfBots, SwitchBehaviourToBot, BotInputGetter);
			if (HandlingClientsInServer)
				GatherInputsFromServerBotsOrClient(_playersOfClients, SwitchBehaviourToClient, ClientInputGetter);

			_inputList.Clear();
			foreach (var inputBufferPair in _gameEngineAdapter.PlayerInputBuffers)
				if (inputBufferPair.Value.TryGetDataForTick(_tick, out var input))
					_inputList.Add(input);
			elympicsBehavioursManager.SetCurrentInputs(_inputList);
		}

		private static ElympicsInput ClientInputGetter(ElympicsBehavioursManager manager) => manager.OnInputForClient();
		private static ElympicsInput BotInputGetter(ElympicsBehavioursManager manager)    => manager.OnInputForBot();

		private void GatherInputsFromServerBotsOrClient(ElympicsPlayer[] players, Action<ElympicsPlayer> switchElympicsBaseBehaviour, Func<ElympicsBehavioursManager, ElympicsInput> onInput)
		{
			foreach (var playerOfBotOrClient in players)
			{
				switchElympicsBaseBehaviour(playerOfBotOrClient);
				var input = onInput(elympicsBehavioursManager);
				input.Tick = _tick;
				input.Player = playerOfBotOrClient;
				_gameEngineAdapter.AddBotsOrClientsInServerInputToBuffer(input, playerOfBotOrClient);
			}

			SwitchBehaviourToServer();
		}

		protected override void LateFixedUpdate()
		{
			if (ShouldSendSnapshot(_tick))
			{
				// gather state info from scene and send to clients
				var snapshots = elympicsBehavioursManager.GetSnapshotsToSend(_gameEngineAdapter.Players);
				AddMetadataToSnapshots(snapshots, _tick);
				_gameEngineAdapter.SendSnapshotsUnreliable(snapshots);
			}

			_tick++;

			foreach (var (_, inputBuffer) in _gameEngineAdapter.PlayerInputBuffers)
				inputBuffer.UpdateMinTick(_tick);
		}

		private bool ShouldSendSnapshot(int tick) => tick % Config.SnapshotSendingPeriodInTicks == 0;

		private void AddMetadataToSnapshots(Dictionary<ElympicsPlayer, ElympicsSnapshot> snapshots, int tick)
		{
			foreach (var (_, snapshot) in snapshots)
				snapshot.Tick = tick;
		}

		private void SwitchBehaviourToServer()
		{
			_currentPlayer = ServerPlayer;
			_currentIsClient = false;
			_currentIsBot = false;
		}

		private void SwitchBehaviourToBot(ElympicsPlayer player)
		{
			_currentPlayer = player;
			_currentIsClient = false;
			_currentIsBot = true;
		}

		private void SwitchBehaviourToClient(ElympicsPlayer player)
		{
			_currentPlayer = player;
			_currentIsClient = true;
			_currentIsBot = false;
		}

		#region IElympics

		public override void EndGame(ResultMatchPlayerDatas result = null) => _gameEngineAdapter.EndGame(result);

		#endregion
	}
}