using System;
using UnityEngine;

namespace CamoHuntAR
{
    [Serializable]
    public sealed class CamouflageRegionBinding
    {
        [SerializeField] private string regionId;
        [SerializeField] private Renderer renderer;
        [SerializeField] private int materialIndex;

        public CamouflageRegionBinding(string regionId, Renderer renderer, int materialIndex)
        {
            this.regionId = regionId;
            this.renderer = renderer;
            this.materialIndex = materialIndex;
        }

        public string RegionId => regionId;
        public Renderer Renderer => renderer;
        public int MaterialIndex => materialIndex;
    }
}
