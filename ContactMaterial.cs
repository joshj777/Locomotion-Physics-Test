using System;
using UnityEngine;

namespace LocomotionTest.GamePhysics
{
    /// <summary>
    /// This component is meant to be placed onto objects that the player can make contact with.
    /// </summary>
    public class ContactMaterial : MonoBehaviour
    {
        [Header("Physic Material Settings")]
        /// <summary> Bounciness value of 0 means no bounce, 1 means that the player/object will maintain all of its previous velocity in the opposite direction. </summary>
        [Range(0, 5)] public float bounciness = 0f;
        /// <summary> slipperiness percentage of 0 means no slipperiness, and 1 means the player will maintain its velocity in the respective move direction. </summary>
        [Range(0, 1)] public float slipPercentage = 0f;
        /// <summary> The player's current strength of their hands will be multipled by this value. </summary>
        [Range(0, 5)] public float strength = 1f;
    }
}