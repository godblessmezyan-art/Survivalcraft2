using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>
    /// Guarantees the four Four Saints outfits exist as native clothing data (200-203) even when
    /// the `.clo` merge pipeline does not deliver them. x25.01.17 loads `.clo` files from the mod
    /// archive into the vanilla clothing XML inside <c>ClothingBlock.Initialize</c>; any single
    /// failure there (null root on load order, an unreadable texture, a malformed element) aborts
    /// the remaining entries, which is why some or all of the outfits silently disappeared from
    /// the clothing tab. Registration here is additive: entries provided by the `.clo` are kept
    /// untouched and only the missing ones are created, one guarded `try` per item so a single
    /// bad asset can never remove the other three.
    /// </summary>
    internal static class FourSaintsClothing
    {
        private sealed class Spec
        {
            internal int Index;
            internal string DisplayName;
            internal string Description;
            internal ClothingSlot Slot;
            internal string TexturePath;
            internal int Layer;
        }

        private static readonly Spec[] s_specs =
        {
            new Spec { Index = FourSaintsRules.QinglongIndex, DisplayName = "青龙冠", Slot = ClothingSlot.Head, Layer = 1, TexturePath = "Textures/Clothing/FourSaintsQinglong",
                Description = "苍龙不息：每5秒恢复1点生命、免疫跌落伤害、最大生命增加5点。" },
            new Spec { Index = FourSaintsRules.XuanwuIndex, DisplayName = "玄武铠", Slot = ClothingSlot.Torso, Layer = 4, TexturePath = "Textures/Clothing/FourSaintsXuanwu",
                Description = "镇岳玄甲：免疫溺水与严寒伤害，每次受到攻击时最终生命伤害减少3点。" },
            new Spec { Index = FourSaintsRules.BaihuIndex, DisplayName = "白虎胫甲", Slot = ClothingSlot.Legs, Layer = 4, TexturePath = "Textures/Clothing/FourSaintsBaihuLegs",
                Description = "西极杀伐：移动与攻击频率提升20%，跳跃高度增加约1格；可发动5秒杀伐觉醒。" },
            new Spec { Index = FourSaintsRules.ZhuqueIndex, DisplayName = "朱雀靴", Slot = ClothingSlot.Feet, Layer = 1, TexturePath = "Textures/Clothing/FourSaintsZhuque",
                Description = "涅槃圣焰：免疫火焰、岩浆与燃烧；濒死时涅槃并短暂无敌。" }
        };

        private static bool s_registered;

        /// <summary>Runs once, after every block finished initializing.</summary>
        internal static void EnsureRegistered()
        {
            if (s_registered)
            {
                return;
            }

            ClothingBlock block = FindClothingBlock();
            if (block == null)
            {
                Log.Warning("[SCIEPort] Clothing block was not initialized; Four Saints outfits skipped.");
                return;
            }

            s_registered = true;
            int created = 0;
            for (int i = 0; i < s_specs.Length; i++)
            {
                if (TryRegister(block, s_specs[i]))
                {
                    created++;
                }
            }

            Log.Information("[SCIEPort] Four Saints clothing ready: " + created + " created, " +
                (s_specs.Length - created) + " already provided by FourSaints.clo.");
        }

        private static bool TryRegister(ClothingBlock block, Spec spec)
        {
            // The .clo file wins when it loaded: never overwrite data authored there.
            if (spec.Index < block.m_clothingData.Count && block.m_clothingData[spec.Index] != null)
            {
                return false;
            }

            try
            {
                if (block.m_clothingData.Count <= spec.Index)
                {
                    block.m_clothingData.Count = spec.Index + 1;
                }

                block.m_clothingData[spec.Index] = new ClothingData
                {
                    Index = spec.Index,
                    DisplayIndex = 1000 + spec.Index,
                    DisplayName = spec.DisplayName,
                    Description = spec.Description,
                    Slot = spec.Slot,
                    ArmorProtection = 1f,
                    Sturdiness = spec.Slot == ClothingSlot.Torso ? 480f : spec.Slot == ClothingSlot.Legs ? 360f : spec.Slot == ClothingSlot.Feet ? 340f : 320f,
                    Insulation = 2f,
                    MovementSpeedFactor = spec.Slot == ClothingSlot.Legs ? 1.2f : 1f,
                    SteedMovementSpeedFactor = 1f,
                    DensityModifier = spec.Slot == ClothingSlot.Torso ? 0.5f : spec.Slot == ClothingSlot.Legs ? 0.35f : 0.2f,
                    IsOuter = true,
                    CanBeDyed = false,
                    Layer = spec.Layer,
                    PlayerLevelRequired = 1,
                    Texture = LoadTexture(spec),
                    ImpactSoundsFolder = "Audio/Impacts/Metal"
                };
                return true;
            }
            catch (System.Exception exception)
            {
                Log.Error("[SCIEPort] Failed to register Four Saints clothing " + spec.DisplayName + " (" + spec.Index + "): " + exception);
                return false;
            }
        }

        /// <summary>A missing texture must not remove the outfit: fall back to the first outfit
        /// texture that does load, and log the exact path so the asset can be fixed.</summary>
        private static Texture2D LoadTexture(Spec spec)
        {
            try
            {
                return ContentManager.Get<Texture2D>(spec.TexturePath);
            }
            catch (System.Exception exception)
            {
                Log.Error("[SCIEPort] Clothing texture failed to load: " + spec.TexturePath + " (" + exception.Message + ")");
            }

            for (int i = 0; i < s_specs.Length; i++)
            {
                if (s_specs[i] == spec)
                {
                    continue;
                }

                try
                {
                    return ContentManager.Get<Texture2D>(s_specs[i].TexturePath);
                }
                catch (System.Exception)
                {
                }
            }

            return null;
        }

        private static ClothingBlock FindClothingBlock()
        {
            Block[] blocks = BlocksManager.Blocks;
            for (int i = 0; i < blocks.Length; i++)
            {
                ClothingBlock clothing = blocks[i] as ClothingBlock;
                if (clothing != null)
                {
                    return clothing;
                }
            }

            return null;
        }
    }
}
