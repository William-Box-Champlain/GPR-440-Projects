using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

public struct GOSpawnData
{
    public Dictionary<eDirection, int> sockets;
    public (int, int, int) gridPosition;
    public float offset;
    public int rotation;
    public UnityEngine.GameObject mObj;
}

public class WFCGameObject : UnityEngine.MonoBehaviour, IWaveformSpawnable<GOSpawnData>, IComparable
{
    [UnityEngine.Header("Sockets")]
    [UnityEngine.SerializeField] int mLeftSocket;
    [UnityEngine.SerializeField] int mRightSocket;
    [UnityEngine.SerializeField] int mFrontSocket;
    [UnityEngine.SerializeField] int mBackSocket;
    public eWFC_Tile_Type tileType;
    GOTile mTile;

    public int CompareTo(object obj)
    {
        throw new NotImplementedException();
    }

    public void Spawn(GOSpawnData spawnData)
    {
        throw new NotImplementedException();
    }

    public GOTile GetTile()
    {
        return mTile;
    }
}

public class GOTile : WFCTile<GOSpawnData>
{
    public GOTile(GOSpawnData mValue) : base(mValue)
    {
    }

    public override int CompareTo(object obj)
    {
        return 0;
    }

    public override void Spawn(GOSpawnData spawnData)
    {
        UnityEngine.Vector3 position = new UnityEngine.Vector3
            (
                mValue.offset * mValue.gridPosition.Item1, 
                mValue.offset * mValue.gridPosition.Item2, 
                mValue.offset * mValue.gridPosition.Item3
            );
        UnityEngine.Quaternion rotation = new UnityEngine.Quaternion();
        rotation.eulerAngles.Set(0, 90 * mValue.rotation, 0);
        UnityEngine.GameObject.Instantiate(mValue.mObj, position, rotation);
    }
}