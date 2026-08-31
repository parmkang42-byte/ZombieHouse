using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// The bone references for one zombie, shared by everything that needs to pose or
    /// break the body. Pivots are empty transforms at joint positions; the limb meshes
    /// hang below them, so rotating a pivot swings the limb from the joint.
    /// </summary>
    [System.Serializable]
    public class ZombieBones
    {
        [Header("Spine")]
        public Transform Rig;
        public Transform Pelvis;
        public Transform Spine;
        public Transform Neck;
        public Transform Head;

        [Header("Legs")]
        public Transform HipLeft;
        public Transform KneeLeft;
        public Transform HipRight;
        public Transform KneeRight;

        [Header("Arms")]
        public Transform ShoulderLeft;
        public Transform ElbowLeft;
        public Transform ShoulderRight;
        public Transform ElbowRight;

        public bool IsComplete
        {
            get
            {
                return Rig != null && Pelvis != null && Spine != null && Head != null
                       && HipLeft != null && KneeLeft != null && HipRight != null && KneeRight != null
                       && ShoulderLeft != null && ElbowLeft != null
                       && ShoulderRight != null && ElbowRight != null;
            }
        }
    }

    /// <summary>Holds the rig so ZombieVisuals, ZombieAppearance and ZombieRagdoll share one copy.</summary>
    public class ZombieRig : MonoBehaviour
    {
        public ZombieBones Bones = new ZombieBones();
    }
}
