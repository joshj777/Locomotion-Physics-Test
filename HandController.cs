using LocomotionTest.GamePhysics;
using UnityEngine;

namespace LocomotionTest.Movement
{
    /// <summary>
    /// This class will be attached to both of the player's hands. This is responsible for controlling some of the logic behind the locomotion.
    /// </summary>
    public class HandController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MovementController movementController;

        [Space]
        [Tooltip("Reference to the VR controller transform.")]
        [SerializeField] private Transform sourceTransform;

        [Space]
        [Tooltip("Reference to the in-game hand transform.")]
        [SerializeField] private Transform targetTransform;
        [Tooltip("Reference to the offset transform for the hand (target). Reference to the shoulder is recommended.")]
        [SerializeField] private Transform offsetTransform;

        [Header("Properties")]
        public Vector3 lastHandPosition { get; set; }

        /// Whether the hand was touching a surface in the last frame.
        public bool wasHandTouching { get; set; }

        public ContactMaterial contactMaterial { get; set; }

        #region Setup Methods
        public void Initialize()
        {
            lastHandPosition = targetTransform.position;

        }

        #endregion Setup Methods

        #region Update Methods
        public void UpdateHandIteration(out Vector3 handMovement, out bool handColliding)
        {
            Vector3 distanceTraveled = sourceTransform.position - lastHandPosition;
            handMovement = Vector3.zero;
            handColliding = false;

            if (movementController.IterativeCollisionSphereCast(lastHandPosition, movementController.handRadius, distanceTraveled, movementController.defaultPrecision, out Vector3 finalPosition, out RaycastHit hitInfo, true))
            {
                //this lets you stick to the position you touch, as long as you keep touching the surface this will be the zero point for that hand
                if (wasHandTouching)
                {
                    handMovement = lastHandPosition - CurrentHandPosition(); //CurrentLeftHandPosition();
                }
                else
                {
                    handMovement = finalPosition - CurrentHandPosition(); //CurrentLeftHandPosition();
                }

                movementController.GetRigidbody().linearVelocity = Vector3.zero;

                handColliding = true;
            }
        }

        public void AttemptUnstick(bool colliding, out bool isColliding)
        {
            isColliding = colliding;

            Vector3 headPosition = movementController.GetHeadCollider().transform.position;
            if (colliding && (CurrentHandPosition() - lastHandPosition).magnitude > movementController.unstickDistance)
            {
                bool pullingTowardsBody = false;
                if (Vector3.Distance(sourceTransform.position, headPosition) < Vector3.Distance(lastHandPosition, headPosition))
                    pullingTowardsBody = true;

                if (pullingTowardsBody)
                {
                    lastHandPosition = CurrentHandPosition();
                    isColliding = false;
                }

                ///We are only moving the hand when pulling towards the body because we 
                ///don't want the player to somehow cheat and pull their hand through objects.
            }
        }

        #endregion Update Methods

        #region Position Update
        public void ResetTargetPosition()
        {
            targetTransform.position = sourceTransform.position;
        }

        public Vector3 CurrentHandPosition()
        {
            //TODO: Rework this clamping system because I'm not sure that this works that well

            Vector3 shoulderPos = offsetTransform.position;

            if (Vector3.Distance(shoulderPos, sourceTransform.position) > movementController.GetArmLength())
            {
                Vector3 directionFromShoulderToHand = sourceTransform.position - shoulderPos;
                Vector3 newPos = shoulderPos + directionFromShoulderToHand.normalized * movementController.GetArmLength();
                return sourceTransform.position = newPos;
            }

            return sourceTransform.position;
        }

        #endregion Position Update

        #region Getters
        public Transform GetSourceTransform() => sourceTransform;
        public Transform GetTargetTransform() => targetTransform;

        #endregion Getters

    }   
}