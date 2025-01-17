﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Elympics
{
	[RequireComponent(typeof(ElympicsBehaviour))]
	public class ElympicsMonoBehaviour : MonoBehaviour, IObservable
	{
		private ElympicsBehaviour _elympicsBehaviour;
		private ElympicsBase      _elympics;
		private ElympicsFactory   _factory;

		public ElympicsBehaviour ElympicsBehaviour
		{
			get
			{
				if ((object) _elympicsBehaviour != null)
					return _elympicsBehaviour;

				if (TryGetComponent(out _elympicsBehaviour))
					return _elympicsBehaviour;

				var missingComponentMessage = $"{GetType().Name} has missing {nameof(ElympicsBehaviour)} component";
				Debug.LogError(missingComponentMessage, this);
				throw new MissingComponentException(missingComponentMessage);
			}
		}

		internal ElympicsBase ElympicsBase
		{
			get
			{
				if ((object) _elympics != null)
					return _elympics;

				_elympics = ElympicsBehaviour.ElympicsBase;

				if (_elympics != null)
					return _elympics;

				var elympicsBaseNullReferenceMessage = $"Calling for Elympics in ElympicsMonoBehaviour before Elympics field is initialized. Check Script Execution Order!";
				Debug.LogError(elympicsBaseNullReferenceMessage, this);
				throw new UnassignedReferenceException(elympicsBaseNullReferenceMessage);
			}
		}

		public bool           IsPredictableForMe => ElympicsBehaviour.IsPredictableTo(Elympics.Player);
		public ElympicsPlayer PredictableFor     => ElympicsBehaviour.predictableFor;

		/// <summary>Checks if Behaviour is enabled and its Game Object is active in hierarchy.</summary>
		/// <remarks>
		/// Works before OnEnabled is called and should be used instead of <see cref="Behaviour.isActiveAndEnabled"/>
		/// for example in <see cref="IInitializable.Initialize"/>.
		/// </remarks>
		public bool IsEnabledAndActive => enabled && gameObject.activeInHierarchy;

		/// <summary>
		/// Provides Elympics-specific game instance data and methods.
		/// </summary>
		/// <exception cref="MissingComponentException">ElympicsBehaviour component is not attached to the object.</exception>
		public IElympics Elympics => ElympicsBase;

		/// <summary>
		/// Synchronize a prefab instantiation and process all its ElympicsBehaviour components.
		/// </summary>
		/// <param name="pathInResources">Path to instantiated prefab which must reside in Resources.</param>
		/// <param name="player">Instantiated object should be predictable for this player.</param>
		/// <returns>Created game object.</returns>
		/// <remarks>For object destruction see <see cref="ElympicsDestroy"/>.</remarks>
		public GameObject ElympicsInstantiate(string pathInResources, ElympicsPlayer player)
		{
			ThrowIfCalledInWrongContextWithPlayer(player);
			return GetFactory().CreateInstance(pathInResources, player);
		}

		[SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
		private void ThrowIfCalledInWrongContextWithPlayer(ElympicsPlayer player, [CallerMemberName] string caller = "")
		{
			if (player == ElympicsPlayer.World && !Elympics.IsServer)
				throw new ElympicsException($"You cannot use {caller} with {player} option as a client or bot");
			if (player != ElympicsPlayer.All && Elympics.IsClient && Elympics.Player != player)
				throw new ElympicsException($"You cannot use {caller} with {player} option as a client {Elympics.Player}");
			ThrowIfCalledInWrongContext(caller);
		}

		/// <summary>
		/// Synchronize a game object destruction.
		/// </summary>
		/// <param name="createdGameObject">Destroyed game object.</param>
		/// <remarks>Only objects instantiated with <see cref="ElympicsInstantiate"/> may be destroyed with this method.</remarks>
		public void ElympicsDestroy(GameObject createdGameObject)
		{
			ThrowIfCalledInWrongContext();
			GetFactory().DestroyInstance(createdGameObject);
		}

		private void ThrowIfCalledInWrongContext([CallerMemberName] string caller = "")
		{
			if (ElympicsBase.CurrentCallContext != ElympicsBase.CallContext.ElympicsUpdate
					&& ElympicsBase.CurrentCallContext != ElympicsBase.CallContext.Initialize)
				throw new ElympicsException($"You cannot use {caller} outside of {nameof(IUpdatable.ElympicsUpdate)} or {nameof(IInitializable.Initialize)}");
		}

		private ElympicsFactory GetFactory() => _factory ?? (_factory = FindObjectOfType<ElympicsFactory>());
	}
}
