using UnityEngine;

namespace BiteOrBetray.Art3D
{
    [DefaultExecutionOrder(20), RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class ChibiFace : MonoBehaviour
    {
        SkinnedMeshRenderer face;
        MaterialPropertyBlock properties;
        int blink = -1, open = -1, chew = -1;

        void Awake()
        {
            face = GetComponent<SkinnedMeshRenderer>();
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < face.sharedMesh.blendShapeCount; i++)
            {
                string key = face.sharedMesh.GetBlendShapeName(i).Split('.')[^1];
                if (key == "Blink") blink = i;
                if (key == "Open") open = i;
                if (key == "Chew") chew = i;
            }
        }

        void LateUpdate()
        {
            face.GetPropertyBlock(properties);
            properties.SetFloat("_FaceBlink", Weight(blink));
            properties.SetFloat("_FaceMouth", Mathf.Max(Weight(open), Weight(chew) * .7f));
            face.SetPropertyBlock(properties);
        }

        float Weight(int index) => index < 0 ? 0 : Mathf.Clamp01(face.GetBlendShapeWeight(index) / 100);

        void OnDisable()
        {
            if (face == null) return;
            face.GetPropertyBlock(properties);
            properties.SetFloat("_FaceBlink", 0);properties.SetFloat("_FaceMouth", 0);
            face.SetPropertyBlock(properties);
        }
    }
}
