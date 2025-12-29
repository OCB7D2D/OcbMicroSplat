using HarmonyLib;

public class BugfixDecorateRoads
{

    // ####################################################################
    // ####################################################################

    // When Cars are spawned as decoration over roads, some blocks underneath
    // are not properly setup, e.g. it shows as `terrForrest` instead of 
    // `terrAsphalt`, since multi-dim decorations will prematurely
    // mark upcoming blocks as `EnumDecoAllowedSize.NoBigNoSmall`.

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(WorldDecoratorPOIFromImage), "DecorateChunkOverlapping")]
    class DecoChunkAddDecoObject2
    {
        static bool Prefix(WorldDecoratorPOIFromImage __instance, World _world,
            Chunk _chunk, Chunk _c10, Chunk _c01, Chunk _c11, int seed)
        {
            if (__instance.m_Poi == null)
                return false;
            if (_c10 == null || _c01 == null || _c11 == null)
            {
                Log.Warning("Adjacent chunk missing on decoration " + _chunk?.ToString());
            }
            else
            {
                GameRandom gameRandom = Utils.RandomFromSeedOnPos(_chunk.X, _chunk.Z, seed);
                GameManager.Instance.GetSpawnPointList();
                Chunk chunk = _chunk;
                Vector3i worldPos = chunk.GetWorldPos();
                bool flag = __instance.m_PrefabDecorator.IsWithinTraderArea(_chunk.worldPosIMin, _chunk.worldPosIMax);
                for (int index1 = 0; index1 < 16; ++index1)
                {
                    int blockWorldPosZ = chunk.GetBlockWorldPosZ(index1);
                    for (int index2 = 0; index2 < 16; ++index2)
                    {
                        int blockWorldPosX = chunk.GetBlockWorldPosX(index2);
                        if ((!flag || __instance.m_PrefabDecorator.GetTraderAtPosition(new Vector3i(blockWorldPosX, 0, blockWorldPosZ), 0) == null) && __instance.m_Poi.Contains(blockWorldPosX, blockWorldPosZ))
                        {
                            byte data = __instance.m_Poi.GetData(blockWorldPosX, blockWorldPosZ);
                            switch (data)
                            {
                                case 0:
                                case byte.MaxValue:
                                    continue;
                                default:
                                    PoiMapElement poiForColor = _world.Biomes.getPoiForColor((uint)data);
                                    if (poiForColor != null)
                                    {
                                        EnumDecoAllowed decoAllowedAt = chunk.GetDecoAllowedAt(index2, index1);
                                        if (!poiForColor.m_BlockValue.isWater)
                                        {
                                            if (!decoAllowedAt.IsNothing())
                                                chunk.SetDecoAllowedStreetOnlyAt(index2, index1, true);
                                            else
                                            {
                                                // Actual Bugfix is here, setup biome top block anyway
                                                BlockValue blockValue = poiForColor.m_BlockValue;
                                                int _y2 = poiForColor.m_YPos;
                                                if (_y2 < 0) _y2 = (int)chunk.GetTerrainHeight(index2, index1);
                                                BlockValue block2 = chunk.GetBlock(index2, _y2, index1);
                                                if (block2.isair || block2.Block.shape.IsTerrain() && blockValue.Block.shape.IsTerrain())
                                                    chunk.SetBlockRaw(index2, _y2, index1, blockValue);
                                                // End of actual Bugfix
                                                continue;
                                            }
                                        }
                                        int _y1 = poiForColor.m_YPos;
                                        if (_y1 < 0)
                                            _y1 = (int)chunk.GetTerrainHeight(index2, index1);
                                        if (!poiForColor.m_BlockValue.isair)
                                        {
                                            BlockValue blockValue = poiForColor.m_BlockValue;
                                            PoiMapDecal randomDecal = poiForColor.GetRandomDecal(gameRandom);
                                            BlockValue block1;
                                            if (randomDecal != null && (double)_chunk.GetTerrainNormalY(index2, index1) > 0.98000001907348633)
                                            {
                                                block1 = _world.GetBlock(new Vector3i(blockWorldPosX, _y1, blockWorldPosZ) + new Vector3i(Utils.BlockFaceToVector(randomDecal.face)));
                                                if (block1.isair)
                                                {
                                                    blockValue.hasdecal = true;
                                                    blockValue.decalface = randomDecal.face;
                                                    blockValue.decaltex = (byte)randomDecal.textureIndex;
                                                }
                                            }
                                            BlockValue block2 = chunk.GetBlock(index2, _y1, index1);
                                            if (block2.isair || block2.Block.shape.IsTerrain() && blockValue.Block.shape.IsTerrain())
                                                chunk.SetBlockRaw(index2, _y1, index1, blockValue);
                                            if (poiForColor.m_BlockValue.isWater)
                                            {
                                                for (int _y2 = _y1; _y2 <= poiForColor.m_YPosFill; ++_y2)
                                                {
                                                    if (WaterUtils.CanWaterFlowThrough(chunk.GetBlock(index2, _y2, index1)))
                                                        chunk.SetWater(index2, _y2, index1, WaterValue.Full);
                                                }
                                            }
                                            else
                                            {
                                                for (int _y3 = _y1; _y3 <= poiForColor.m_YPosFill; ++_y3)
                                                {
                                                    block1 = chunk.GetBlock(index2, _y3, index1);
                                                    if (block1.isair)
                                                        chunk.SetBlockRaw(index2, _y3, index1, blockValue);
                                                }
                                            }
                                            if (block2.Block.shape.IsTerrain() && !poiForColor.m_BlockBelow.isair)
                                                chunk.SetBlockRaw(index2, _y1, index1, poiForColor.m_BlockBelow);
                                            if (poiForColor.m_YPosFill > 0 && __instance.bChangeWaterDensity)
                                            {
                                                if (!blockValue.Block.shape.IsTerrain())
                                                    chunk.SetDensity(index2, _y1, index1, MarchingCubes.DensityAir);
                                                for (int _y4 = _y1; _y4 <= poiForColor.m_YPosFill; ++_y4)
                                                {
                                                    chunk.SetBlockRaw(index2, _y4, index1, blockValue);
                                                    if (!blockValue.Block.shape.IsTerrain())
                                                        chunk.SetDensity(index2, _y4, index1, MarchingCubes.DensityAir);
                                                }
                                            }
                                            PoiMapBlock randomBlockOnTop = poiForColor.GetRandomBlockOnTop(gameRandom);
                                            if (randomBlockOnTop != null)
                                            {
                                                if (randomBlockOnTop.offset == 0)
                                                {
                                                    block1 = _world.GetBlock(new Vector3i(blockWorldPosX, _y1 + 1, blockWorldPosZ));
                                                    if (!block1.isair)
                                                        continue;
                                                }
                                                blockValue = _world.IsEditor() ? randomBlockOnTop.blockValue : BlockPlaceholderMap.Instance.Replace(randomBlockOnTop.blockValue, gameRandom, _chunk, blockWorldPosX, 0, blockWorldPosZ, FastTags<TagGroup.Global>.none);
                                                int num = _y1 + 1 + randomBlockOnTop.offset;
                                                Vector3i vector3i = new Vector3i(worldPos.x + index2, worldPos.y + num, worldPos.z + index1);
                                                if (DecoUtils.CanPlaceDeco(_chunk, _c10, _c01, _c11, vector3i, blockValue))
                                                {
                                                    DecoUtils.ApplyDecoAllowed(_chunk, _c10, _c01, _c11, vector3i, blockValue);
                                                    Block block3 = blockValue.Block;
                                                    blockValue = block3.OnBlockPlaced((WorldBase)_world, 0, vector3i, blockValue, gameRandom);
                                                    if (!block3.HasTileEntity)
                                                    {
                                                        chunk.SetBlockRaw(index2, num, index1, blockValue);
                                                        continue;
                                                    }
                                                    chunk.SetBlock((WorldBase)_world, index2, num, index1, blockValue);
                                                    continue;
                                                }
                                                continue;
                                            }
                                            continue;
                                        }
                                        if (poiForColor.m_sModelName != null && poiForColor.m_sModelName.Length > 0 && __instance.m_PrefabDecorator != null)
                                        {
                                            __instance.m_PrefabDecorator.GetPrefab(poiForColor.m_sModelName).CopyIntoLocal(_world.ChunkClusters[0], new Vector3i(blockWorldPosX, _y1, blockWorldPosZ), false, false, FastTags<TagGroup.Global>.none);
                                            continue;
                                        }
                                        continue;
                                    }
                                    continue;
                            }
                        }
                    }
                }
                GameRandomManager.Instance.FreeGameRandom(gameRandom);
            }
            return false;
        }
    }

    // ####################################################################
    // ####################################################################

}

