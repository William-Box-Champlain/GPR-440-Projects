using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public class WFCManager : MonoBehaviour
{
    [SerializeField] private Vector3Int mWaveformSize;
    PrototypeGenerator<(int, int, int), GOSpawnData, GameObject> mPrototypeGenerator;
    Waveform<(int, int, int), GOSpawnData> mWaveform;
    IObjectSelector<IPrototypeTile<(int,int,int),GOSpawnData>> mObjectSelector;

    List<GameObject> mTileObjects;

    IEnumerable<WFCTilePrototype<(int,int,int),GOSpawnData>> PrototypeRotationRule(WFCTile<GOSpawnData> tile)
    {
        HashSet<WFCTilePrototype<(int, int, int), GOSpawnData>> output = new HashSet<WFCTilePrototype<(int, int, int), GOSpawnData>>();
        for(int i = 0; i < 4;i++)
        {
            GameObjectWaveformNode temp = new GameObjectWaveformNode();
            GOSpawnData tempData = new GOSpawnData();
            tempData.rotation = i;
            temp.SpawnData = tempData;

            //output.Add(temp)
        }
        return output;
    }

    IEnumerable<WFCTilePrototype<(int, int, int), GOSpawnData>> PrototypeFlipRule(WFCTile<GOSpawnData> tile)
    {
        HashSet<WFCTilePrototype<(int, int, int), GOSpawnData>> output = new HashSet<WFCTilePrototype<(int, int, int), GOSpawnData>>();
        //TODO:
        return output;
    }

    Dictionary<(int,int,int), IWaveformNode<(int,int,int),GOSpawnData>> GenerateNodes((int,int,int) size, IEnumerable<WFCTilePrototype<(int,int,int),GOSpawnData>> prototypes, System.Random rand)
    {
        Dictionary<(int, int, int), IWaveformNode<(int, int, int), GOSpawnData>> output = new Dictionary<(int, int, int), IWaveformNode<(int, int, int), GOSpawnData>>();
        
        //create nodes
        for (int i = 0; i < mWaveformSize.x; i++)
        {
            for (int j = 0; j < mWaveformSize.y; j++)
            {
                for (int  k = 0; k < mWaveformSize.z; k++)
                {
                    GameObjectWaveformNode temp = new GameObjectWaveformNode();

                    temp.Parent = mWaveform;
                    temp.Selector = mObjectSelector;
                    GOSpawnData tempData = new GOSpawnData();
                    tempData.gridPosition = (i, j, k);
                    temp.SpawnData = tempData;

                    foreach(var prototype in prototypes)
                    {
                        temp.ValidStates.Add(prototype);
                    }

                    output.Add((i,j,k),temp);
                }
            }
        }
        //create neighbor relationships
        //TODO:
        foreach (var node in output)
        {
            foreach (eDirection dir in Enum.GetValues(typeof(eDirection)))
            {
                //break up position
                int x = node.Key.Item1;
                int y = node.Key.Item2;
                int z = node.Key.Item3;

                //modify based on direction I'm going to check
                switch (dir)
                {
                    case eDirection.Left:
                        x--;
                        break;
                    case eDirection.Right:
                        x++;
                        break;
                    case eDirection.Front:
                        y++;
                        break;
                    case eDirection.Back:
                        y--;
                        break;
                }
                //check for node at position
                IWaveformNode<(int,int,int),GOSpawnData> temp = null;
                if (output.TryGetValue((x, y, z), out temp))
                {
                    node.Value.Neighbors.Add((x, y, z), temp);
                }
            }
        }
        return output;
    }

    private void Start()
    {
        var temp = new GameObjectPrototypeGenerator();
        mPrototypeGenerator = temp as PrototypeGenerator<(int, int, int), GOSpawnData, GameObject>;

        mPrototypeGenerator.AddRule(PrototypeFlipRule);
        mPrototypeGenerator.AddRule(PrototypeRotationRule);

        foreach (var baseTile in mTileObjects)
        {
            mPrototypeGenerator.CreatePrototypeTile(baseTile);
        }

        mObjectSelector = new SimpleRandomSelector<IPrototypeTile<(int, int, int), GOSpawnData>>();

        mWaveform = new Waveform<(int, int, int), GOSpawnData>();
        mWaveform.GenerateNodes(GenerateNodes,(mWaveformSize.x,mWaveformSize.y,mWaveformSize.z),mPrototypeGenerator.GetPrototypes(),new System.Random());
    }
}

public class GameObjectPrototypeGenerator : PrototypeGenerator<(int, int, int), GOSpawnData, GameObject>
{
    protected override WFCTile<GOSpawnData> CreateBaseTile(GameObject obj)
    {
        return obj.GetComponent<WFCGameObject>().GetTile();
    }
}

public class GameObjectWaveformNode : IWaveformNode<(int, int, int), GOSpawnData>
{
    public GameObjectWaveformNode()
    {
        mNeighbors = new Dictionary<(int, int, int), IWaveformNode<(int, int, int), GOSpawnData>>();
        mValidStates = new HashSet<IPrototypeTile<(int, int, int), GOSpawnData>>();
        mParent = null;
        mSelector = null;
        mSpawnData = default;
    }

    Dictionary<(int, int, int), IWaveformNode<(int, int, int), GOSpawnData>> mNeighbors;
    HashSet<IPrototypeTile<(int, int, int), GOSpawnData>> mValidStates;
    Waveform<(int, int, int), GOSpawnData> mParent;
    IObjectSelector<IPrototypeTile<(int, int, int), GOSpawnData>> mSelector;
    GOSpawnData mSpawnData;
    public Dictionary<(int, int, int), IWaveformNode<(int, int, int), GOSpawnData>> Neighbors 
    {
        get => mNeighbors; 
        set => mNeighbors = value; 
    }
    public HashSet<IPrototypeTile<(int, int, int), GOSpawnData>> ValidStates 
    { 
        get => mValidStates; 
        set => mValidStates = value; 
    }
    public Waveform<(int, int, int), GOSpawnData> Parent 
    { 
        get => mParent; 
        set => mParent = value; 
    }
    public IObjectSelector<IPrototypeTile<(int, int, int), GOSpawnData>> Selector 
    { 
        get => mSelector; 
        set => mSelector = value; 
    }
    public GOSpawnData SpawnData 
    { 
        get => mSpawnData; 
        set => mSpawnData = value; 
    }
    public (int, int, int) Opposite((int, int, int) direction)
    {
        return (-direction.Item1,-direction.Item2,-direction.Item3);
    }
}