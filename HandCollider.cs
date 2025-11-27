using System;
using UnityEngine;

namespace TestProject.Movement 
{
    /// <summary>
    /// This is an extra component that I was working on originally for the locomotion system but I ended up not using it.
    /// Keeping it here just in case any work can be done to it.
    /// </summary>
    public class HandCollider : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SphereCollider Collider;

        [Header("Settings")]
        [SerializeField] private LayerMask groundMask;

        [Header("Events")]
        public Action<Collider> OnContactEnter;
        public Action<Collider> OnContactExit;

        [Header("Properties")]
        [SerializeField] private Collider _lastContact;

        #region Collision Events
        private void OnCollisionEnter(Collision collision)
        {
            Collider collider = collision.collider;
            Debug.Log("HandCollider OnCollisionEnter with " + collider.name);

            if (collider.gameObject.layer != groundMask || collider == _lastContact)
                return;

            _lastContact = collider;
            OnContactEnter?.Invoke(collider);
        }

        private void OnCollisionExit(Collision collision)
        {
            Collider collider = collision.collider;

            if (collider != _lastContact)
                return;

            _lastContact = null;
            OnContactExit?.Invoke(collider);
        }

        public void OnTriggerStay(Collider other)
        {
            if (other.gameObject.layer != groundMask)
                return;
        }

        #endregion Collision Events
    }
}