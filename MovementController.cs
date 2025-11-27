using LocomotionTest.GamePhysics;
using UnityEngine;

namespace LocomotionTest.Movement
{
    /// <summary>
    /// This class handles all of the main movement logic for the player.
    /// </summary>
    public class MovementController : MonoBehaviour
    {
        public bool _initialized { get; private set; }

        [Header("References")]
        [SerializeField] private HandController leftController;
        [SerializeField] private HandController rightController;

        [Space]
        [SerializeField] private GameplayTimer gameplayTimer;

        [Header("Components")]
        [SerializeField] private Rigidbody bodyRigidbody;
        [SerializeField] private CapsuleCollider bodyCollider;

        [Space]
        [SerializeField] private SphereCollider headCollider;

        [Header("Movement Settings")]
        public bool _movementEnabled = true;

        [Header("Physics Settings")]
        public LayerMask groundMask;

        [Space]
        public float handRadius = 0.15f;
        public float headRadius = 0.2f;

        [Space]
        public float defaultPrecision = 0.995f;

        [Space]
        public float defaultSlideFactor = 0.03f;

        [Space]
        /// <summary> This is the distance the hand must be from the source to be in a state of unsticking. </summary>
        public float unstickDistance = 1.25f;

        [Header("Velocity Settings")]
        [Tooltip("The amount of 'force' a hand must apply for it to be applied as a velocity to the body.")]
        [SerializeField] private float velocityLimit = 2.75f;

        [Space]
        [Tooltip("Whenever forces are being applied to the body through hand movements, this is the maximum velocity that can be physically applied by the hands.")]
        [SerializeField] private float maxForceVelocity = 14f;

        [Tooltip("This is how much force will be multiplied to the velocity of the hand.")]
        [SerializeField] private float forceMultiplier = 1f;

        [Space]
        ///<summary> A low history size value is recommended for more responsive movement. </summary>
        [SerializeField] private int velocityHistorySize = 6;
        
        private Vector3[] velocityHistory;
        public int velocityIndex = 0;

        [Header("Arm Settings")]
        /// <summary> The maximum length of the user's arms. (NOTE: This has not been fully implemented.) </summary>
        [SerializeField] private float armLength = 3.65f;

        [Header("Properties")]
        public Vector3 currentVelocity;
        /// <summary> The average of the stored velocities. This is equal to the CURRENT velocity subtracted by the LAST stored velocity matching the same index as the current velocity, divided by the history size. </summary>
        public Vector3 denormalizedVelocityAverage;

        private Vector3 lastHeadPosition;
        /// <summary> The last position of the body's rigidbody. </summary>
        private Vector3 lastPosition;

        /// <summary> The last hand that collided with a surface. </summary>
        public HandController lastHandCollided { get; private set; }

        [Header("Debugging")]
        public bool _finalizedHandPosition;

        [Header("Gizmos Debugging")]
        public bool _drawGizmos = true;
        public bool _drawInEditor = false;

        [Space]
        public bool _drawHandColliders = true;
        public bool _drawHandMovementVector = true;
        public bool _drawHeadCollider = true;

        [Space]
        /// <summary> Whenever contact is made by the hands, this gizmo will display the contact point. </summary>
        public bool _drawSphereCastRays = true;
        private float _direction;
        private Vector3 _originPos;
        private Vector3 _hitPos;
        private float _sphereRadius;

        [Space]
        /// <summary> Will display both the current and last velocity for the body. </summary>
        public bool _debugVelocity = true;

        private void Start()
        {
            Initialize();
        }

        #region Runtime Methods
        public void Initialize()
        {
            if (_initialized)
                return;

            velocityHistory = new Vector3[velocityHistorySize];

            leftController.Initialize();
            rightController.Initialize();

            lastHeadPosition = headCollider.transform.position;
            lastPosition = bodyRigidbody.position;

            //_initialized = true;
        }

        #endregion Runtime Methods

        #region Update
        private void Update()
        {
            ///This is necessary for updating Gizmos debugging.
            ResetGizmzoDebugProperties();

            gameplayTimer.UpdateTick(Time.deltaTime);
            if (!gameplayTimer.ShouldTick())
                return;

            ///SECTION 1: Hand Movement
            leftController.UpdateHandIteration(out Vector3 leftHandMovement, out bool leftHandColliding);
            rightController.UpdateHandIteration(out Vector3 rightHandMovement, out bool rightHandColliding);

            bool singleHand = !((leftHandColliding || leftController.wasHandTouching) && (rightHandColliding || rightController.wasHandTouching));
            Vector3 rigidBodyMovement;

            if (!singleHand)
            {
                //This averages the movement when both hands are colliding for smoother movement
                rigidBodyMovement = (leftHandMovement + rightHandMovement) / 2f;
            }
            else
            {
                rigidBodyMovement = leftHandMovement + rightHandMovement;
            }

            ///SECTION 2: Reconciling the Head (Checks if the head is passing through geometry and moves it back if so)
            ReconcileHeadTransform(rigidBodyMovement, out rigidBodyMovement);

            //Applying the movement to the body
            if (rigidBodyMovement != Vector3.zero)
                bodyRigidbody.transform.position += rigidBodyMovement;

            lastHeadPosition = headCollider.transform.position;

            ///SECTION 3: Finalizing Hand Positions
            FinalizeHandPosition(leftController, rigidBodyMovement, singleHand, out bool _leftHandCollided);
            FinalizeHandPosition(rightController, rigidBodyMovement, singleHand, out bool _rightHandCollided);
            
            leftHandColliding = true ? _leftHandCollided == true : false;
            rightHandColliding = true ? _rightHandCollided == true : false;

            if (!leftHandColliding && !rightHandColliding)
                lastHandCollided = null;

            ///SECTION 4: Updating Velocity
            StoreVelocity();

            if ((rightHandColliding || leftHandColliding) && _movementEnabled)
            {
                UpdateBodyVelocity();
            }

            //?SECTION 5: If hands are stuck, we will attempt to fix them using these methods
            leftController.AttemptUnstick(leftHandColliding, out leftHandColliding);
            rightController.AttemptUnstick(rightHandColliding, out rightHandColliding);

            ///SECTION 6: Setting the target transforms to the final positions
            leftController.GetTargetTransform().position = leftController.lastHandPosition;
            rightController.GetTargetTransform().position = rightController.lastHandPosition;

            leftController.wasHandTouching = leftHandColliding;
            rightController.wasHandTouching = rightHandColliding;
        }

        /*private void Update()
        {
            RunGameDebugCode();
        }*/

        #endregion Update

        #region Velocity Management
        private void StoreVelocity()
        {
            velocityIndex = (velocityIndex + 1) % velocityHistorySize;
            Vector3 oldestVelocity = velocityHistory[velocityIndex];

            currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
            denormalizedVelocityAverage += (currentVelocity - oldestVelocity) / (float)velocityHistorySize;

            velocityHistory[velocityIndex] = currentVelocity;
            lastPosition = transform.position;

            //DEBUGGING
            if (_debugVelocity && Application.isPlaying && velocityHistory.Length > 0)
            {
                Vector3 origin = headCollider.transform.position;
                Debug.DrawLine(origin, origin + currentVelocity, Color.magenta, 0f, false);
                Debug.DrawLine(origin, origin + oldestVelocity, Color.yellow, 0f, false);
            }
        }

        private void UpdateBodyVelocity()
        {
            if (denormalizedVelocityAverage.magnitude > velocityLimit)
            {
                if (denormalizedVelocityAverage.magnitude * forceMultiplier > maxForceVelocity)
                {
                    bodyRigidbody.linearVelocity = denormalizedVelocityAverage.normalized * maxForceVelocity;
                }
                else
                {
                    bodyRigidbody.linearVelocity = forceMultiplier * denormalizedVelocityAverage;

                    if (bodyRigidbody.linearVelocity.magnitude > maxForceVelocity)
                    {
                        bodyRigidbody.linearVelocity = bodyRigidbody.linearVelocity.normalized * maxForceVelocity;
                    }
                }

            }
        }

        #endregion Velocity Management

        #region Hand Updating
        /// <summary>
        /// This method is responsible for the actual setting of the hand's final position and contact material. 
        /// </summary>
        private void FinalizeHandPosition(HandController controller, Vector3 offset, bool singleHand, out bool handColliding)
        {
            Vector3 handPosition = controller.GetSourceTransform().position;
            Vector3 lastHandPosition = controller.lastHandPosition;
            handColliding = false;

            Vector3 distanceTraveled = handPosition - (offset) - lastHandPosition;
            Debug.DrawLine(handPosition, handPosition + (distanceTraveled.normalized * 1), Color.green, 0f);

            if (IterativeCollisionSphereCast(lastHandPosition, handRadius, distanceTraveled, defaultPrecision, out Vector3 finalPosition, out RaycastHit hitInfo, singleHand))
            {
                controller.lastHandPosition = finalPosition;
                handColliding = true;

                _finalizedHandPosition = true;
            }
            else
            {
                controller.lastHandPosition = handPosition;
            }

            if (!controller.wasHandTouching && handColliding)
            {
                //Setting the last hand collided
                lastHandCollided = controller;

                //Setting the contact material
                if (hitInfo.transform != null && hitInfo.transform.TryGetComponent(out ContactMaterial material))
                {
                    controller.contactMaterial = material;
                }
            }
            else if (controller.wasHandTouching && !handColliding)
            {
                controller.contactMaterial = null;
            }
        }

        #endregion Hand Updating

        #region Head Transform Management
        /// <summary>
        /// Checks if the head is about to clip through geometry and adjusts the body movement to prevent this.
        /// </summary>
        private void ReconcileHeadTransform(Vector3 rigidBodyMovement, out Vector3 newBodyMovement)
        {
            Vector3 direction = headCollider.transform.position + rigidBodyMovement - headCollider.transform.position;
            newBodyMovement = rigidBodyMovement;

            if (IterativeCollisionSphereCast(lastHeadPosition, headRadius, direction, defaultPrecision, out Vector3 hitPos, out RaycastHit hitInfo, false))
            {
                newBodyMovement = hitPos - lastHeadPosition;

                //Extra check to prevent the head from clipping through geometry
                if (Physics.Raycast(lastHeadPosition, direction, out hitInfo, (headCollider.transform.position - lastHeadPosition + rigidBodyMovement).magnitude + headCollider.radius * defaultPrecision, groundMask.value))
                {
                    //move the body rigidbody back along the hit normal to prevent clipping
                    newBodyMovement = lastHeadPosition - headCollider.transform.position;
                }
            }
        }

        #endregion Head Transform Management

        #region Raycasting
        public bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 endPosition, out RaycastHit hitInfo, bool singleHand)
        {
            Vector3 movementToProjectedAboveCollisionPlane;
            float slipPercentage;

            //first spherecast from the starting position to the final position
            if (CollisionsSphereCast(startPosition, sphereRadius * precision, movementVector, precision, out endPosition, out hitInfo))
            {
                //if we hit a surface, do a bit of a slide. this makes it so if you grab with two hands you don't stick 100%, and if you're pushing along a surface while braced with your head, your hand will slide a bit

                //take the surface normal that we hit, then along that plane, do a spherecast to a position a small distance away to account for moving perpendicular to that surface
                ContactMaterial surface = hitInfo.collider.GetComponent<ContactMaterial>();
                slipPercentage = surface != null ? surface.slipPercentage : (!singleHand ? defaultSlideFactor : 0.001f);

                Vector3 firstPosition = endPosition;
                movementToProjectedAboveCollisionPlane = Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, hitInfo.normal) * slipPercentage;
                if (CollisionsSphereCast(endPosition, sphereRadius, movementToProjectedAboveCollisionPlane, precision * precision, out endPosition, out hitInfo))
                {
                    //if we hit trying to move perpendicularly, stop there and our end position is the final spot we hit
                    return true;
                }
                //if not, try to move closer towards the true point to account for the fact that the movement along the normal of the hit could have moved you away from the surface
                else if (CollisionsSphereCast(movementToProjectedAboveCollisionPlane + firstPosition, sphereRadius, startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition), precision * precision * precision, out endPosition, out hitInfo))
                {
                    //if we hit, then return the spot we hit
                    return true;
                }
                else
                {
                    //this shouldn't really happen, since this means that the sliding motion got you around some corner or something and let you get to your final point. back off because something strange happened, so just don't do the slide
                    endPosition = firstPosition;
                    return true;
                }
            }
            //as kind of a sanity check, try a smaller spherecast. this accounts for times when the original spherecast was already touching a surface so it didn't trigger correctly
            else if (CollisionsSphereCast(startPosition, sphereRadius * precision * 0.66f, movementVector.normalized * (movementVector.magnitude + sphereRadius * precision * 0.34f), precision * 0.66f, out endPosition, out hitInfo))
            {
                endPosition = startPosition;
                return true;
            }
            else
            {
                endPosition = Vector3.zero;
                return false;
            }
        }

        /// <summary>
        /// Performs a spherecast that accounts for the radius of the sphere when colliding with surfaces. Will return a position that maintains the radius size.
        /// </summary>
        public bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 finalPosition, out RaycastHit hitInfo)
        {
            //Includes checks to make sure that the sphere we're using, if it touches a surface, is pushed away the correct distance (the original sphereradius distance).
            //Since you might be pushing into sharp corners, this might not always be valid, so that's what the extra checks are for.

            RaycastHit innerHit;
            ///Performs the first spherecast from the start position to the intended end position
            if (Physics.SphereCast(startPosition, sphereRadius * precision, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * (1 - precision), groundMask.value))
            {
                //if we hit, we're trying to move to a position a sphereradius distance from the normal
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                //Gizmos Debugging (FOR TESTING PURPOSES ONLY)
                _direction = movementVector.magnitude * sphereRadius * (1 - precision);
                _originPos = startPosition;
                _hitPos = finalPosition;
                _sphereRadius = sphereRadius * precision;
                //---------------------------------------

                //check a spherecase from the original position to the intended final position
                if (Physics.SphereCast(startPosition, sphereRadius * precision * precision, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision), groundMask.value))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized * Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                //bonus raycast check to make sure that something odd didn't happen with the spherecast. helps prevent clipping through geometry
                else if (Physics.Raycast(startPosition, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * precision * precision * 0.999f, groundMask.value))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                    return true;
                }
                return true;
            }
            //anti-clipping through geometry check
            else if (Physics.Raycast(startPosition, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * precision * 0.999f, groundMask.value))
            {
                finalPosition = startPosition;
                return true;
            }
            else
            {
                finalPosition = Vector3.zero;
                return false;
            }
        }

        #endregion Raycasting

        #region Debug
        private void RunGameDebugCode()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                bodyRigidbody.linearVelocity = Vector3.zero;
                bodyRigidbody.transform.position = new Vector3(0, 4, 0);
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                Debug.Break();
            }
        }

        private void ResetGizmzoDebugProperties()
        {
            if (_drawSphereCastRays)
            {
                _direction = 0;
                _originPos = Vector3.zero;
                _hitPos = Vector3.zero;
                _sphereRadius = 0;
            }

            _finalizedHandPosition = false;
        }

        public void OnDrawGizmos()
        {
            if (!_drawGizmos || (!Application.isPlaying && !_drawInEditor))
                return;

            if (_drawHeadCollider)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(headCollider.transform.position + headCollider.center, headCollider.radius);
            }

            if (_drawHandColliders)
            {
                float radius = handRadius * defaultPrecision;

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(leftController.GetTargetTransform().position, radius);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(rightController.GetTargetTransform().position, radius);
            }

            if (_drawHandMovementVector)
            {
                float length = 2f;
                if (leftController.wasHandTouching)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(leftController.lastHandPosition, leftController.lastHandPosition + (denormalizedVelocityAverage.normalized * length));
                }

                if (rightController.wasHandTouching)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(rightController.lastHandPosition, rightController.lastHandPosition + (denormalizedVelocityAverage.normalized * length));
                }

            }

            if (_drawSphereCastRays)
            {
                if (_hitPos != Vector3.zero)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(_originPos, _hitPos);
                    Gizmos.DrawWireSphere(_hitPos, _sphereRadius);
                }

                if (_direction != 0)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(_originPos, _originPos + (_direction * (_hitPos - _originPos).normalized));
                }
            }

            if (lastHandCollided != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(lastHandCollided.lastHandPosition, handRadius * 1.2f);
            }

            
        }

        #endregion Debug

        #region Getters
        public Rigidbody GetRigidbody() => bodyRigidbody;
        public CapsuleCollider GetBodyCollider() => bodyCollider;
        public SphereCollider GetHeadCollider() => headCollider;

        public HandController GetLeftController() => leftController;
        public HandController GetRightController() => rightController;

        public float GetArmLength() => armLength;

        #endregion Getters

    }   
}