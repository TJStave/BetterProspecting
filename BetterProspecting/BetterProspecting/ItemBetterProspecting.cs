﻿using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.API.Datastructures;

namespace BetterProspecting
{
    public class ItemBetterProspecting : ItemProspectingPick
    {
        SkillItem[]? toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            //I need to add assets!

            ICoreClientAPI? capi = api as ICoreClientAPI;
            toolModes = ObjectCacheUtil.GetOrCreate(api, "proPickToolModes", () =>
            {
                SkillItem[] modes;
                modes = new SkillItem[5];
                modes[0] = new SkillItem() { Code = new AssetLocation("distance"), Name = Lang.Get("Distance Mode (Long range, distance search)") };
                modes[1] = new SkillItem() { Code = new AssetLocation("stone"), Name = Lang.Get("Stone Mode (Long range, distance search for stone)") };
                modes[2] = new SkillItem() { Code = new AssetLocation("area1"), Name = Lang.Get("Area Sample Mode (Searches in a small area)") };
                modes[3] = new SkillItem() { Code = new AssetLocation("area2"), Name = Lang.Get("Area Sample Mode (Searches in a medium area)") };
                modes[4] = new SkillItem() { Code = new AssetLocation("area3"), Name = Lang.Get("Area Sample Mode (Searches in a large area)") };

                if (capi != null)
                {
                    modes[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("betterprospecting", "textures/icons/abpro_line.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[0].TexturePremultipliedAlpha = false;
                    modes[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("betterprospecting", "textures/icons/abpro_stone.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[1].TexturePremultipliedAlpha = false;
                    modes[2].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("betterprospecting", "textures/icons/abpro_small.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[2].TexturePremultipliedAlpha = false;
                    modes[3].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("betterprospecting", "textures/icons/abpro_med.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[3].TexturePremultipliedAlpha = false;
                    modes[4].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("betterprospecting", "textures/icons/abpro_large.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[4].TexturePremultipliedAlpha = false;
                }

                return modes;
            });

            base.OnLoaded(api);
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return Math.Min(toolModes.Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"));
        }

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            float remain = base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
            int toolMode = GetToolMode(itemslot, player, blockSel);

            remain = (remain + remainingResistance) / 2.2f;
            return remain;
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            int toolMode = GetToolMode(itemslot, (byEntity as EntityPlayer).Player, blockSel);
            int damage = 4;

            if (toolMode == 0) {
                ProbeDistanceSampleMode(world, byEntity, itemslot, blockSel, (int)EnumProspectingArea.DirectionalArea, (int)EnumProspectingArea.Ycoords, toolMode);
            }
            else if (toolMode == 1)
            {
                ProbeDistanceSampleMode(world, byEntity, itemslot, blockSel, (int)EnumProspectingArea.DirectionalArea / 2, (int)EnumProspectingArea.Ycoords, toolMode);
            }
            else if (toolMode == 2)
            {
                 ProbeAreaSampleMode(world, byEntity, itemslot, blockSel, (int)EnumProspectingArea.SmallArea, (int)EnumProspectingArea.Ycoords);
            }
            else if (toolMode == 3)
            {
                 ProbeAreaSampleMode(world, byEntity, itemslot, blockSel, (int)EnumProspectingArea.MediumArea, (int)EnumProspectingArea.Ycoords);
            }
            else if (toolMode == 4)
            {
                 ProbeAreaSampleMode(world, byEntity, itemslot, blockSel, (int)EnumProspectingArea.LargeArea, (int)EnumProspectingArea.Ycoords);
            }

            if (DamagedBy != null && DamagedBy.Contains(EnumItemDamageSource.BlockBreaking))
            {
                DamageItem(world, byEntity, itemslot, damage);
            }

            return true;
        }

        protected virtual void ProbeAreaSampleMode(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, int xzlength, int ylength)
        {
            IPlayer? byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            block.OnBlockBroken(world, blockSel.Position, byPlayer, 0);

            if (!isPropickable(block)) return;

            IServerPlayer? serverPlayer = byPlayer as IServerPlayer;
            if (serverPlayer == null) return;

            serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, $"Area sample taken for a length of {xzlength}:"), EnumChatType.Notification);

            Dictionary<string, int> quantityFound = new Dictionary<string, int>();

            BlockPos blockPos = blockSel.Position.Copy();
            api.World.BlockAccessor.WalkBlocks(blockPos.AddCopy(xzlength, ylength, xzlength), blockPos.AddCopy(-xzlength, -ylength, -xzlength), delegate (Block nblock, int x, int y, int z)
            {
                if (nblock.BlockMaterial == EnumBlockMaterial.Ore && nblock.Variant.ContainsKey("type"))
                {
                    string key = "ore-" + nblock.Variant["type"];
                    int value = 0;
                    quantityFound.TryGetValue(key, out value);
                    quantityFound[key] = value + 1;
                }
            });
            List<KeyValuePair<string, int>> list = quantityFound.OrderByDescending((KeyValuePair<string, int> val) => val.Value).ToList();
            if (list.Count == 0)
            {
                serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, "No ore node nearby"), EnumChatType.Notification);
                return;
            }

            serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, "Found the following ore nodes"), EnumChatType.Notification);
            foreach (KeyValuePair<string, int> item in list)
            {
                string l = Lang.GetL(serverPlayer.LanguageCode, item.Key);
                string l2 = Lang.GetL(serverPlayer.LanguageCode, resultTextByQuantity(item.Value), Lang.Get(item.Key));
                serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, l2, l), EnumChatType.Notification);
            }
        }

        protected virtual void ProbeDistanceSampleMode(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, int xzlength, int ylength, int mode)
        {
            IPlayer? byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            block.OnBlockBroken(world, blockSel.Position, byPlayer, 0);

            if (!isPropickable(block)) return;

            IServerPlayer? serverPlayer = byPlayer as IServerPlayer;
            if (serverPlayer == null) return;

            serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, $"Area sample taken for a length of {xzlength}:"), EnumChatType.Notification);

            Dictionary<string, int> firstOreDistance = new Dictionary<string, int>();

            BlockPos blockPos = blockSel.Position.Copy();
            api.World.BlockAccessor.WalkBlocks(blockPos.AddCopy(xzlength, ylength, xzlength), blockPos.AddCopy(-xzlength, -ylength, -xzlength), delegate (Block nblock, int x, int y, int z)
            {
                if (mode == 0 && nblock.BlockMaterial == EnumBlockMaterial.Ore && nblock.Variant.ContainsKey("type"))
                {
                    string key = nblock.Variant["type"].ToUpper();
                    int distance = (int)blockSel.Position.DistanceTo(new BlockPos(x, y, z));
                    if (!firstOreDistance.ContainsKey(key) || distance < firstOreDistance[key])
                    {
                        firstOreDistance[key] = distance;
                    }
                }
                if (mode == 1 && nblock.Variant.ContainsKey("rock"))
                {
                    string key = nblock.Variant["rock"].ToUpper();
                    int distance = (int)blockSel.Position.DistanceTo(new BlockPos(x, y, z));
                    if (!firstOreDistance.ContainsKey(key) || distance < firstOreDistance[key])
                    {
                        firstOreDistance[key] = distance;
                    }
                }

            });

            List<KeyValuePair<string,int>> list = firstOreDistance.ToList();
            if (list.Count == 0)
            {
                serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, "No ore node nearby"), EnumChatType.Notification);
                return;
            }

            serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, "Found the following ore nodes"), EnumChatType.Notification);
            foreach (KeyValuePair<string, int> item in list)
            {
                serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(serverPlayer.LanguageCode, $"{item.Key}: {item.Value} block(s) away"), EnumChatType.Notification);
            }
        }

        private bool isPropickable(Block block)
        {
            return block?.Attributes?["propickable"].AsBool(false) == true;
        }
    }
}
