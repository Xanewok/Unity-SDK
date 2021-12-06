﻿using UnityEngine;
using Cinemachine;
using Elympics;

namespace TechDemo
{
	[RequireComponent(typeof(PlayerBehaviour))]
	public class PlayerInputController : ElympicsMonoBehaviour, IInitializable, IInputHandler
	{
		[SerializeField] private CinemachineVirtualCamera    virtualCamera         = null;
		[SerializeField] private PlayerJoystickInputProvider joystickInputProvider = null;
		[SerializeField] private PlayerBotInputProvider      botInputProvider      = null;

		private PlayerBehaviour _playerBehaviour;

		// Handling only one player through this input handlers, every player has the same player input controller
		public void GetInputForClient(IInputWriter inputSerializer)
		{
			joystickInputProvider.GetRawInput(out var forwardMovement, out var rightMovement, out var fire);
			SerializeInput(inputSerializer, forwardMovement, rightMovement, fire);
		}

		public void GetInputForBot(IInputWriter inputSerializer)
		{
			botInputProvider.GetRawInput(out var forwardMovement, out var rightMovement, out var fire);
			SerializeInput(inputSerializer, forwardMovement, rightMovement, fire);
		}

		private static void SerializeInput(IInputWriter inputWriter, float forwardMovement, float rightMovement, bool fire)
		{
			inputWriter.Write(forwardMovement);
			inputWriter.Write(rightMovement);
			inputWriter.Write(fire);
		}

		public void ApplyInput(ElympicsPlayer player, IInputReader inputReader)
		{
			inputReader.Read(out float forwardMovement);
			inputReader.Read(out float rightMovement);
			inputReader.Read(out bool fire);

			if (player != _playerBehaviour.PredictableFor)
				return;

			if (fire)
				_playerBehaviour.Fire();
			_playerBehaviour.Move(forwardMovement, rightMovement);
		}

		public void Initialize()
		{
			_playerBehaviour = GetComponent<PlayerBehaviour>();
			InitializeVirtualCamera();
		}

		private void InitializeVirtualCamera()
		{
			// Initialize camera only to player played by us
			if (Elympics.Player != _playerBehaviour.PredictableFor)
				return;

			var target = _playerBehaviour.transform;
			virtualCamera.LookAt = target;
			virtualCamera.Follow = target;
		}
	}
}