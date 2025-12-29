using HarmonyLib;
using System.Collections.Generic;

class BugfixChangeBlocks
{

    // ####################################################################
    // ####################################################################

    [HarmonyPatch(typeof(GameManager), "ChangeBlocks")]
    class GameManagerChangeBlocks
    {
        static bool Prefix(GameManager __instance,
            PlatformUserIdentifierAbs persistentPlayerId,
            List<BlockChangeInfo> _blocksToChange)
        {
            if (__instance.m_World == null)
                return false;
            lock (__instance.ccChanged)
            {
                PersistentPlayerData persistentPlayerData = (PersistentPlayerData)null;
                Entity entity = (Entity)null;
                if (persistentPlayerId == null)
                {
                    persistentPlayerData = __instance.persistentLocalPlayer;
                    entity = (Entity)__instance.myEntityPlayerLocal;
                }
                else if (__instance.persistentPlayers != null)
                {
                    persistentPlayerData = __instance.persistentPlayers.GetPlayerData(persistentPlayerId);
                    if (persistentPlayerData != null && persistentPlayerData.EntityId != -1)
                        entity = __instance.m_World.GetEntity(persistentPlayerData.EntityId);
                }
                bool flag1 = false;
                bool flag2 = false;
                ChunkCluster chunkCluster = (ChunkCluster)null;
                int count1 = 0;
                for (int index = 0; index < _blocksToChange.Count; ++index)
                {
                    BlockChangeInfo blockChangeInfo = _blocksToChange[index];
                    if (chunkCluster == null)
                    {
                        chunkCluster = __instance.m_World.ChunkCache;
                        if (chunkCluster != null)
                        {
                            if (!__instance.ccChanged.Contains(chunkCluster))
                            {
                                __instance.ccChanged.Add(chunkCluster);
                                ++count1;
                                chunkCluster.ChunkPosNeedsRegeneration_DelayedStart();
                            }
                        }
                        else
                            continue;
                    }
                    bool _isChangeDensity = blockChangeInfo.bChangeDensity;
                    bool forceDensityChange = blockChangeInfo.bForceDensityChange;
                    sbyte density = chunkCluster.GetDensity(blockChangeInfo.pos);
                    sbyte _density = blockChangeInfo.density;
                    if (!_isChangeDensity)
                    {
                        if (density < (sbyte)0 && blockChangeInfo.blockValue.isair)
                        {
                            _density = MarchingCubes.DensityAir;
                            _isChangeDensity = true;
                        }
                        else if (density >= (sbyte)0 && blockChangeInfo.blockValue.Block.shape.IsTerrain())
                        {
                            _density = MarchingCubes.DensityTerrain;
                            _isChangeDensity = true;
                        }
                    }
                    if ((int)density == (int)_density)
                        _isChangeDensity = false;
                    if (!blockChangeInfo.bChangeDamage || chunkCluster.GetBlock(blockChangeInfo.pos).type == blockChangeInfo.blockValue.type)
                    {
                        Chunk chunkFromWorldPos = chunkCluster.GetChunkFromWorldPos(blockChangeInfo.pos) as Chunk;
                        int blockX = World.toBlockXZ(blockChangeInfo.pos.x);
                        int blockZ = World.toBlockXZ(blockChangeInfo.pos.z);
                        if (chunkFromWorldPos != null)
                        {
                            if (blockChangeInfo.pos.y >= (int)chunkFromWorldPos.GetHeight(World.toBlockXZ(blockChangeInfo.pos.x), World.toBlockXZ(blockChangeInfo.pos.z)) && blockChangeInfo.blockValue.Block.shape.IsTerrain())
                            {
                                chunkFromWorldPos.SetTopSoilBroken(blockX, blockZ);
                                // Fixes applied by ocbMaurice (actually use the newly requested adjacent chunk instances)
                                (blockZ != 15 ? chunkFromWorldPos : chunkCluster.GetChunkSync(chunkFromWorldPos.X, chunkFromWorldPos.Z + 1))?.SetTopSoilBroken(blockX, World.toBlockXZ(blockZ + 1));
                                (blockX != 15 ? chunkFromWorldPos : chunkCluster.GetChunkSync(chunkFromWorldPos.X + 1, chunkFromWorldPos.Z))?.SetTopSoilBroken(World.toBlockXZ(blockX + 1), blockZ);
                                (blockZ != 0 ? chunkFromWorldPos : chunkCluster.GetChunkSync(chunkFromWorldPos.X, chunkFromWorldPos.Z - 1))?.SetTopSoilBroken(blockX, World.toBlockXZ(blockZ - 1));
                                (blockX != 0 ? chunkFromWorldPos : chunkCluster.GetChunkSync(chunkFromWorldPos.X - 1, chunkFromWorldPos.Z))?.SetTopSoilBroken(World.toBlockXZ(blockX - 1), blockZ);
                            }
                            __instance.m_World.UncullChunk(chunkFromWorldPos);
                        }
                        TileEntity tileEntity1 = (TileEntity)null;
                        if (!blockChangeInfo.blockValue.ischild)
                            tileEntity1 = __instance.m_World.GetTileEntity(blockChangeInfo.pos);
                        BlockValue _bvOld = chunkCluster.SetBlock(blockChangeInfo.pos, blockChangeInfo.bChangeBlockValue, blockChangeInfo.blockValue, _isChangeDensity, _density, true, blockChangeInfo.bUpdateLight, forceDensityChange, _changedByEntityId: blockChangeInfo.changedByEntityId);
                        if (tileEntity1 != null)
                        {
                            TileEntity tileEntity2 = __instance.m_World.GetTileEntity(blockChangeInfo.pos);
                            if (tileEntity1 != tileEntity2 && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                            {
                                __instance.lockedTileEntities.Remove((ITileEntity)tileEntity1);
                                tileEntity1.ReplacedBy(_bvOld, blockChangeInfo.blockValue, tileEntity2);
                            }
                            if (blockChangeInfo.blockValue.isair)
                            {
                                __instance.lockedTileEntities.Remove((ITileEntity)tileEntity1);
                                chunkFromWorldPos?.RemoveTileEntityAt<TileEntity>(__instance.m_World, World.toBlock(blockChangeInfo.pos));
                            }
                            else if (tileEntity1 != tileEntity2)
                            {
                                __instance.lockedTileEntities.Remove((ITileEntity)tileEntity1);
                                tileEntity2?.UpgradeDowngradeFrom(tileEntity1);
                            }
                        }
                        if (chunkFromWorldPos != null && blockChangeInfo.blockValue.isair)
                            chunkFromWorldPos.RemoveBlockTrigger(World.toBlock(blockChangeInfo.pos));
                        if (_bvOld.type != blockChangeInfo.blockValue.type)
                        {
                            Block block1 = blockChangeInfo.blockValue.Block;
                            Block block2 = _bvOld.Block;
                            QuestEventManager.Current.BlockChanged(block2, block1, blockChangeInfo.pos);
                            if (block1 is BlockLandClaim)
                            {
                                if (persistentPlayerData != null)
                                {
                                    __instance.persistentPlayers.PlaceLandProtectionBlock(blockChangeInfo.pos, persistentPlayerData);
                                    flag1 = true;
                                    if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                                        ((BlockLandClaim)block1).HandleDeactivatingCurrentLandClaims(persistentPlayerData);
                                    if (__instance.m_World != null && BlockLandClaim.IsPrimary(blockChangeInfo.blockValue))
                                    {
                                        NavObject navObject = NavObjectManager.Instance.RegisterNavObject("land_claim", blockChangeInfo.pos.ToVector3());
                                        if (navObject != null)
                                            navObject.OwnerEntity = entity;
                                    }
                                }
                            }
                            else if (block2 is BlockLandClaim)
                            {
                                __instance.persistentPlayers.RemoveLandProtectionBlock(blockChangeInfo.pos);
                                flag1 = true;
                                flag2 = true;
                                if (__instance.m_World != null)
                                {
                                    NavObjectManager.Instance.UnRegisterNavObjectByPosition(blockChangeInfo.pos.ToVector3(), "land_claim");
                                    if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage((NetPackage)NetPackageManager.GetPackage<NetPackageEntityMapMarkerRemove>().Setup(EnumMapObjectType.LandClaim, blockChangeInfo.pos.ToVector3()));
                                }
                            }
                            if (block1 is BlockSleepingBag || block2 is BlockSleepingBag)
                            {
                                EntityAlive ownerEntity = entity as EntityAlive;
                                if ((bool)(UnityEngine.Object)ownerEntity)
                                {
                                    if (block1 is BlockSleepingBag)
                                    {
                                        NavObjectManager.Instance.UnRegisterNavObjectByOwnerEntity((Entity)ownerEntity, "sleeping_bag");
                                        ownerEntity.SpawnPoints.Set(blockChangeInfo.pos);
                                    }
                                    else
                                        __instance.persistentPlayers.SpawnPointRemoved(blockChangeInfo.pos);
                                    flag1 = true;
                                }
                            }
                        }
                        if (blockChangeInfo.bChangeTexture)
                            chunkCluster.SetTextureFullArray(blockChangeInfo.pos, blockChangeInfo.textureFull);
                        else if (_bvOld.Block.CanBlocksReplace)
                            chunkCluster.SetTextureFullArray(blockChangeInfo.pos, new TextureFullArray(0L));
                    }
                }
                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer & flag1)
                {
                    if (flag2 && (UnityEngine.Object)entity != (UnityEngine.Object)null)
                        entity.PlayOneShot("keystone_destroyed");
                    __instance.SavePersistentPlayerData();
                }
                if (count1 <= 0)
                    return false;
                int count2 = __instance.ccChanged.Count;
                for (int index = 0; index < count1; ++index)
                    __instance.ccChanged[--count2].ChunkPosNeedsRegeneration_DelayedStop();
                __instance.ccChanged.RemoveRange(count2, count1);
            }

            return false;
        }
    }

    // ####################################################################
    // ####################################################################

}
