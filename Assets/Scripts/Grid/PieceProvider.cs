using System;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.Grid
{
    public class PieceProvider
    {
        private List<HexPieceConfig> pieces = new List<HexPieceConfig>();
        private float[] cooldowns;

        public IReadOnlyList<HexPieceConfig> Pieces => pieces;

        public event Action OnHandChanged;

        public PieceProvider(IReadOnlyList<HexPieceConfig> configs, int seed = -1)
        {
            foreach (var config in configs)
            {
                if (config.generationWeight > 0f)
                    pieces.Add(config);
            }

            cooldowns = new float[pieces.Count];
        }

        public void Initialize()
        {
            for (int i = 0; i < cooldowns.Length; i++)
                cooldowns[i] = 0f;

            OnHandChanged?.Invoke();
        }

        public HexPieceType GetPieceType(int index)
        {
            return pieces[index].pieceType;
        }

        public HexPieceConfig GetConfig(int index)
        {
            return pieces[index];
        }

        public bool IsReady(int index)
        {
            return cooldowns[index] <= 0f;
        }

        public float GetCooldownFraction(int index)
        {
            if (pieces[index].placementCooldown <= 0f)
                return 0f;
            return UnityEngine.Mathf.Clamp01(cooldowns[index] / pieces[index].placementCooldown);
        }

        public float GetCooldownRemaining(int index)
        {
            return UnityEngine.Mathf.Max(0f, cooldowns[index]);
        }

        public void StartCooldown(int index)
        {
            // Cooldowns disabled
        }

        public void UpdateCooldowns(float deltaTime)
        {
            bool anyChanged = false;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] > 0f)
                {
                    cooldowns[i] -= deltaTime;
                    if (cooldowns[i] <= 0f)
                    {
                        cooldowns[i] = 0f;
                        anyChanged = true;
                    }
                }
            }

            if (anyChanged)
                OnHandChanged?.Invoke();
        }
    }
}
