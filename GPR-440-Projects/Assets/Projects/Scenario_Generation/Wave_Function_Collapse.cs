using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices.WindowsRuntime;

public enum eWFC_Tile_Type
{
    eEdge,
    eGrass,
    eStraight,
    eCorner,
    eTIntersection,
    eCross,
}

public enum eDirection
{
    Left = 0,
    Front = 1,
    Right = 2,
    Back = 3,
    Up = 4,
    Down = 5,
}

public enum ePossibilityState
{
    Unknown,
    Collapsed,
    Impossible
}

public enum eRotation: uint
{
    Zero = 0,
    Ninety = 90,
    OneEighty = 180,
    TwoSeventy = 270
}

public static class WFCHelpers
{
    public static eDirection Plus(eDirection lhs, eDirection rhs)
    {
        return (eDirection)((int)lhs +  (int)rhs);
    }

    public static eDirection Opposite(eDirection direction)
    {
        return (eDirection)((int)(direction + 2) % 4); // (Left + 2) % 4 = 2 = right, (Back + 2) % 4 = 1 = front, etc
    }
}

public interface IWaveFunctionCollapseTile<T>
{
    public int CompareTo(object obj);
    public void Spawn(T spawnData);
}
public interface IPrototypeTile<V,T> : IWaveformSpawnable<T> where T : struct
{
    T Value { get; set; }
    Dictionary<V,HashSet<IPrototypeTile<V,T>>> ValidNeighbors { get; set;}
    public float GetWeight();
}
public interface IWaveformSpawnable<T> where T : struct //T is the data used for spawning the object.
{
    public void Spawn(T spawnData);
}

/// <summary>
/// 
/// </summary>
/// <typeparam name="T">spawning data</typeparam>
public abstract class WFCTile<T> : IComparable, IWaveformSpawnable<T> where T : struct //If T was for a gameObject, then it should currently contain a delegate for spawning the object
{
    public T mValue;
    public WFCTile(T mValue)
    {
        this.mValue = mValue;
    }

    public abstract int CompareTo(object obj);
    public abstract void Spawn(T spawnData);
}

/// <summary>
/// 
/// </summary>
/// <typeparam name="V">dimension and directionality</typeparam>
/// <typeparam name="T">spawning data</typeparam>
public abstract class WFCTilePrototype<V, T> : IComparable, IWaveFunctionCollapseTile<T>, IPrototypeTile<V, T>, IWaveformSpawnable<T> where T : struct //If T was for a gameObject, then it should have rotational data now
{
    T mValue;
    Dictionary<V, HashSet<IPrototypeTile<V, T>>> mNeighbors;

    public T Value 
    { 
        get => mValue; 
        set => mValue = value; 
    }
    public Dictionary<V, HashSet<IPrototypeTile<V, T>>> ValidNeighbors 
    { 
        get => mNeighbors; 
        set => mNeighbors = value; 
    }

    public abstract float GetWeight();
    public  HashSet<IPrototypeTile<V,T>> GetValidNeighbors(V direction)
    {
        HashSet<IPrototypeTile<V, T>> output = default;

        mNeighbors.TryGetValue(direction, out output);

        return output;
    }

    public abstract void Spawn(T spawnData);

    public abstract int CompareTo(object obj);
}

public delegate IEnumerable<WFCTilePrototype<V,T>> PrototypeGenerationRule<V,T>(WFCTile<T> tile) where T : struct;
/// <summary>
/// 
/// </summary>
/// <typeparam name="V">dimensionality and direction</typeparam>
/// <typeparam name="T">spawning data</typeparam>
/// <typeparam name="U">object type for conversion into WFC Tiles</typeparam>
public abstract class PrototypeGenerator<V,T,U> where T: struct
{
    public List<WFCTile<T>> mBaseTiles;
    public List<WFCTilePrototype<V,T>> mPrototypes;
    private HashSet<PrototypeGenerationRule<V,T>> mRules;

    void GeneratePrototypes(IEnumerable<WFCTile<T>> exampleObjects)
    {
        foreach(var example in exampleObjects)
        {
            foreach(var rule in mRules)
            {
                mPrototypes.Union<WFCTilePrototype<V,T>>(rule(example));
            }
        }
    }

    void GeneratePrototypes(WFCTile<T> exampleObject)
    {
        foreach (var rule in mRules)
        {
            mPrototypes.Union<WFCTilePrototype<V, T>>(rule(exampleObject));
        }
    }

    public void AddRule(PrototypeGenerationRule<V,T> rule)
    {
        mRules.Add(rule);
    }

    public List<WFCTilePrototype<V,T>> GetPrototypes()
    {
        return mPrototypes;
    }

    public void CreatePrototypeTile(U obj)
    {
        GeneratePrototypes(CreateBaseTile(obj));
    }

    protected abstract WFCTile<T> CreateBaseTile(U obj);
}

public interface IObjectSelector<T>
{
    public void AddItem(T item, float weight);
    public void Clear();
    public T GetRandomItem(float choice);
    public float GetTotalWeight();
}

public class SimpleRandomSelector<T> : IObjectSelector<T>
{
    private HashSet<(T, float)> mItems;
    private float mTotalWeight;

    public void AddItem(T item, float weight)
    {
        mItems.Add((item, 1));
        mTotalWeight += 1;
    }

    public void Clear()
    {
        mItems.Clear();
    }

    public T GetRandomItem(float choice)
    {
        float cumulativeWeight = 0f;
        foreach (var (item, weight) in mItems)
        {
            cumulativeWeight += weight;
            if (choice <= cumulativeWeight) return item;
        }

        return default(T);
    }

    public float GetTotalWeight()
    {
        return mTotalWeight;
    }
}

/// <summary>
/// 
/// </summary>
/// <typeparam name="V">dimension and directionality</typeparam>
/// <typeparam name="T">spawning data</typeparam>
public delegate Dictionary<V, IWaveformNode<V, T>> NodeGenerationDelegate<V,T>(V size, IEnumerable<WFCTilePrototype<V, T>> prototypes, Random rand) where T : struct;

/// <summary>
/// 
/// </summary>
/// <typeparam name="V">dimension and directionality</typeparam>
/// <typeparam name="T">spawning data</typeparam>
public class Waveform<V,T> where T : struct
{
    public Random mRand;

    protected Dictionary<V, IWaveformNode<V,T>> mNodes = default;

    //Collapse Algorithm
    #region
    public ePossibilityState Collapse()
    {
        while (CalculateState() == ePossibilityState.Unknown)
        {
            HashSet<IWaveformNode<V,T>> lowestEntropyNodes = GetLowestEntropyNodes();
            IWaveformNode<V,T> node = GetRandomElement(lowestEntropyNodes,mRand);
            node?.Collapse(GetRandomFloat(0, node.GetEntropy(), mRand));
            Propogate(node);
        }
        return CalculateState();
    }
    private ePossibilityState CalculateState()
    {
        ePossibilityState output = ePossibilityState.Collapsed;
        foreach (var node in mNodes)
        {
            switch (node.Value.CalculateState())
            {
                case ePossibilityState.Collapsed:
                    continue;
                case ePossibilityState.Unknown:
                    return ePossibilityState.Unknown;
                case ePossibilityState.Impossible:
                    return ePossibilityState.Impossible;
            }
        }
        return output;
    }
    protected HashSet<IWaveformNode<V,T>> GetLowestEntropyNodes()
    {
        float lowestEntropy = GetLowestEntropy();
        HashSet<IWaveformNode<V,T>> output = new HashSet<IWaveformNode<V,T>>();
        foreach (var node in mNodes)
        {
            if (node.Value.GetEntropy() == lowestEntropy && node.Value.CalculateState() == ePossibilityState.Unknown)
            {
                output.Add(node.Value);
            }
        }
        return output;
    }
    protected float GetLowestEntropy()
    {
        float lowestEntropy = -1.0f;
        foreach (var node in mNodes)
        {
            if (node.Value.CalculateState() != ePossibilityState.Unknown)
            {
                continue;
            }
            if (lowestEntropy == -1.0f)
            {
                lowestEntropy = node.Value.GetEntropy();
            }
            if (node.Value.GetEntropy() < lowestEntropy)
            {
                lowestEntropy = node.Value.GetEntropy();
            }
        }
        return lowestEntropy;
    }
    W GetRandomElement<W>(IEnumerable<W> enumerable, Random rand)
    {
        int index = rand.Next(0, enumerable.Count());
        return enumerable.ElementAt(index);
    }
    float GetRandomFloat(float min, float max, Random rand)
    {
        return (float)rand.NextDouble() * (max - min) + min;
    }
    protected void Propogate(IWaveformNode<V,T> startNode)
    {
        IWaveformNode<V,T> node = startNode;
        Queue<IWaveformNode<V,T>> nodeQueue = CreatePropogationQueue(node);

        while (nodeQueue.Count > 0)
        {
            if (node != null)
            {
                node = nodeQueue.Dequeue();
                node.Propogate();
            }
        }
    }
    protected Queue<IWaveformNode<V, T>> CreatePropogationQueue(IWaveformNode<V,T> startNode)
    {
        Queue<IWaveformNode<V, T>> output = new Queue<IWaveformNode<V,T>>();
        IWaveformNode<V, T> node = null;
        HashSet<IWaveformNode<V, T>> visited = new HashSet<IWaveformNode<V, T>>();

        output.Enqueue(startNode);

        while (output.Count > 0) //While the queue isn't empty
        {
            node = output.Dequeue();
            visited.Add(node);
            foreach (var neighbor in node.Neighbors)
            {
                if (!visited.Contains(neighbor.Value))
                {
                    output.Enqueue(neighbor.Value as IWaveformNode<V, T>);
                }
            }
        }

        foreach (var visitedNode in visited)
        {
            output.Enqueue(visitedNode);
        }

        return output;
    }
    public void Spawn()
    {
        foreach (var node in mNodes)
        {
            node.Value.Spawn();
        }
    }
    #endregion

    //Node Construction
    #region
    public void GenerateNodes(NodeGenerationDelegate<V,T> generationFunction,V volume,IEnumerable<WFCTilePrototype<V,T>> prototypes,Random rand)
    {
        //instantiate nodes
        mNodes = generationFunction(volume,prototypes,rand);
        //assign prototypes to nodes

    }
    #endregion
}

/// <summary>
/// 
/// </summary>
/// <typeparam name="V">dimension and directionality</typeparam>
/// <typeparam name="T">spawning data</typeparam>
public interface IWaveformNode<V,T> where T : struct
{
    //member data
    Dictionary<V,IWaveformNode<V,T>> Neighbors { get; set; }
    HashSet<IPrototypeTile<V, T>> ValidStates { get; set; }
    Waveform<V, T> Parent { get; set; }
    IObjectSelector<IPrototypeTile<V, T>> Selector {get;set;}
    T SpawnData { get; set;}

    //Collapse stuff
    public ePossibilityState CalculateState()
    {
        if (ValidStates.Count > 1) return ePossibilityState.Unknown;
        if (ValidStates.Count <= 0) return ePossibilityState.Impossible;
        if (ValidStates.Count == 1) return ePossibilityState.Collapsed;
        else
            return ePossibilityState.Unknown;
    }
    public void Collapse(float choice)
    {
        IPrototypeTile<V,T> tile = null;
        HashSet<IPrototypeTile<V, T>> collapsedSet = new HashSet<IPrototypeTile<V, T>>();

        Selector.Clear();

        foreach(var prototype in ValidStates) Selector.AddItem(prototype, CalculateWeight(prototype));

        tile = Selector.GetRandomItem(choice);

        collapsedSet.Add(tile);
        ValidStates.IntersectWith(collapsedSet);
    }
    public void Propogate()
    {
        if (CalculateState() == ePossibilityState.Unknown)
        {
            foreach (var neighbor in Neighbors)
            {
                if (neighbor.Value != null)
                {
                    ValidStates.IntersectWith(neighbor.Value.GetValidNeighborStates(Opposite(neighbor.Key)));
                }
            }
        }
    }
    public void Spawn()
    {
        if(CalculateState() == ePossibilityState.Collapsed)
        {
            ValidStates.First().Spawn(SpawnData);
        }
    }
    public V Opposite(V direction);
    //State stuff
    public HashSet<IPrototypeTile<V,T>> GetValidNeighborStates(V direction)
    {
        HashSet<IPrototypeTile<V,T>> output = new HashSet<IPrototypeTile<V, T>>();
        foreach(var prototype in ValidStates)
        {
            HashSet<IPrototypeTile<V,T>> temp = new HashSet<IPrototypeTile<V, T>>();
            prototype.ValidNeighbors.TryGetValue(direction, out temp);
            output.UnionWith(temp);
        }
        return output;
    }
    public IWaveformNode<V,T> GetNeighborsNodes(V direction)
    {
        return Neighbors[direction];
    }
    public float CalculateWeight(IPrototypeTile<V,T> prototype)
    {
        return prototype.GetWeight();
    }
    public float GetEntropy()
    {
        float output = 0;

        foreach(var prototype in ValidStates)
        {
            output += CalculateWeight(prototype);
        }

        return output;
    }
}