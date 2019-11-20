﻿namespace Team1_GraduationGame.Sound
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Team1_GraduationGame.Events;

    public class FootStepSystem : MonoBehaviour
    {

        public string[] materialTypes;
        public IntEvent footStepEvent;

        private void Awake()
        {
            if (materialTypes != null)
                for (int i = 0; i < materialTypes.Length; i++)
                {
                    materialTypes[i] = materialTypes[i].ToLower();
                }
        }

        private void OnTriggerEnter(Collider col)
        {
            if (col.gameObject.GetComponent<Terrain>() != null)
            {
                Terrain thisTerrain = col.gameObject.GetComponent<Terrain>();
                if (thisTerrain.terrainData.terrainLayers.Length > 0)
                {
                    FootStepRaise(thisTerrain.terrainData.terrainLayers[0].diffuseTexture.ToString());
                    Debug.Log(thisTerrain.terrainData.terrainLayers[0].diffuseTexture);
                }
            }

        }

        private void FootStepRaise(string matLayerName)
        {
            if (materialTypes == null)
                return;

            matLayerName = matLayerName.ToLower();

            for (int i = 0; i < materialTypes.Length; i++)
            {
                if (materialTypes[i].Contains(matLayerName))
                {
                    footStepEvent?.Raise(i);
                    return;
                }
            }
        }
    }
}