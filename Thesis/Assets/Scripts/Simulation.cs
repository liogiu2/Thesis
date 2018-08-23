﻿using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;

using ru.cadia.pddlFramework;
public class Simulation : MonoBehaviour
{

    private class SimulationBoundarySorterAcending : IComparer<SimulationBoundary>
    {
        public int Compare(SimulationBoundary x, SimulationBoundary y)
        {
            return x.level.CompareTo(y.level);
        }
    }

    private class SimulationBoundarySorterDescending : IComparer<SimulationBoundary>
    {
        public int Compare(SimulationBoundary x, SimulationBoundary y)
        {
            return y.level.CompareTo(x.level);
        }
    }

    public GameObject player;
    public Visualization visualizer;
    public List<SimulationBoundary> simulationBoundaries;

    private string logFilePath = "Assets/Logs/";
    private string logFileName;
    private Vector3 m_Point;
    private TreeNode<WorldState> _currentNode;
    private int _currentLevelOfDetail;
    // We keep track of the last WorldState that we observed for each LoD 
    private Dictionary<SimulationBoundary, TreeNode<WorldState>> _lastObservedStates;
    // private Dictionary<int, Model> _lastObservedStatesTest;

    public static Action lastActionPerformed;

    public TreeNode<WorldState> CurrentNode
    {
        get { return _currentNode; }
    }

    public int CurrentLevelOfDetail
    {
        get { return _currentLevelOfDetail; }
        set { _currentLevelOfDetail = value; }
    }

    public TreeNode<WorldState> getLastObservedStateAtLevel(int level)
    {
        foreach (KeyValuePair<SimulationBoundary, TreeNode<WorldState>> pair in _lastObservedStates)
            if (pair.Key.level == level)
                return pair.Value;
        return null;
    }
    public void setLastObservedStateAtLevel(int level, TreeNode<WorldState> node)
    {
        foreach (KeyValuePair<SimulationBoundary, TreeNode<WorldState>> pair in _lastObservedStates)
        {
            if (pair.Key.level == level)
            {
                _lastObservedStates[pair.Key] = node;
                break;
            }
        }
    }

    public SimulationBoundary getSimulationBoundaryAtLevel(int level)
    {
        foreach (SimulationBoundary sb in simulationBoundaries)
            if (sb.level == level)
                return sb;
        return null;
    }

    public WorldState parseToLevelOfDetail(WorldState fromState, Domain toLevelOfDetail)
    {

        WorldState resultingState = new WorldState(toLevelOfDetail);

        // add all the possible entities and relations, skip if there is an exception
        // because it means that it cannot be added, as it contrasts with the domain
        foreach (Entity e in fromState.Entities)
        {
            try { resultingState.addEntity(e); }
            catch (System.ArgumentException) { }
        }
        foreach (IRelation r in fromState.Relations)
        {
            try { resultingState.addRelation(r); }
            catch (System.ArgumentException) { }
        }

        return resultingState;
    }

    // Use this for initialization
    void Start()
    {
        System.DateTime localDate = System.DateTime.Now;
        // System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("it-IT");
        logFileName = "algorithm_performances_" + localDate.Year + "-" + localDate.Month + "-" + localDate.Day + "_" +
            localDate.Hour + "-" + localDate.Minute + "-" + localDate.Second + ".log";

        _lastObservedStates = new Dictionary<SimulationBoundary, TreeNode<WorldState>>();
        m_Point = player.transform.position;

        // sort the boundaries according to their priority
        simulationBoundaries.Sort(
            new SimulationBoundarySorterDescending()
        );

        // parse the JSON domain for each simulation boundary and assign the corresponding domain
        foreach (SimulationBoundary sb in simulationBoundaries)
        {
            Domain deserializedDomain = Newtonsoft.Json.JsonConvert.DeserializeObject<Domain>(File.ReadAllText(sb.jsonDomain), new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            });
            sb.domain = deserializedDomain;
        }

        int maxLoD = simulationBoundaries.Max(t => t.level);

        // set the initial state at the most detailed level
        TreeNode<WorldState> initialStateFullDetail = new TreeNode<WorldState>(
            Utils.roverWorldStateThirdLevel(getSimulationBoundaryAtLevel(maxLoD).domain)
        );

        // parse the most detailed state to every level of detaile and initialize
        // the last observed state of each boundary to the initial state
        foreach (SimulationBoundary sb in simulationBoundaries)
        {
            _lastObservedStates.Add(sb, new TreeNode<WorldState>(
                    parseToLevelOfDetail(initialStateFullDetail.Data, sb.domain)
                )
            );
        }

        // iterate over the boundaries and start the simulation on the
        // highest level according to them
        bool playerInBoundaries = false;
        foreach (SimulationBoundary sb in simulationBoundaries)
        {
            if (sb.boundary.bounds.Contains(m_Point))
            {
                Debug.Log("The simulation is starting at level: " + sb.boundary.transform.name);
                _currentNode = getInitialWorldStateAtLevel(sb.level);
                _currentLevelOfDetail = sb.level;
                playerInBoundaries = true;
                break;
            }
        }
        // if the player is outside every boundary start the simulation
        // at the shallowest LoD possible
        if (playerInBoundaries == false)
        {
            SimulationBoundary shallowestLevel = simulationBoundaries.Last();
            _currentNode = getInitialWorldStateAtLevel(shallowestLevel.level);
            _currentLevelOfDetail = shallowestLevel.level;
            Debug.Log("Player was not found inside any boundary, starting simulation at level: " + shallowestLevel.boundary.transform.name);
        }

        StartCoroutine(randomSimulation());
    }

    private IEnumerator randomSimulation()
    {
        int lastLoD = _currentLevelOfDetail;
        while (true)
        {
            // switch the simulation the change of value is controlled
            // by the LevelOfDetailSwitcher script attached to each boundary
            if (_currentLevelOfDetail != lastLoD)
            {
                // find in which boundary the player is at
                SimulationBoundary currentSimulationBoundary = getSimulationBoundaryAtLevel(_currentLevelOfDetail);

                // Refinement
                if (_currentLevelOfDetail > lastLoD)
                {
                    StreamWriter writer = new StreamWriter(logFilePath + logFileName, true);

                    // // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
                    // // this portion is to test normal BFS with no subgoals 
                    // // from last detailed state straight to current abstract

                    // var otherWatch = System.Diagnostics.Stopwatch.StartNew();

                    // TreeNode<WorldState> otherSolution = Utils.breadthFirstSearch(
                    //     _lastObservedStates[currentSimulationBoundary].Data, _currentNode.Data);
                    // otherWatch.Stop();

                    // long otherElapsedMs = otherWatch.ElapsedMilliseconds;

                    // writer.WriteLine("Normal BFS search time: " + otherElapsedMs + " ms\n");
                    // writer.WriteLine("Normal BFS search explored nodes: " + Utils.bfsExploredNodes + "\n");

                    // Stack<string> normalBFSSolution = new Stack<string>();
                    // while (otherSolution.IsRoot == false)
                    // {
                    //     normalBFSSolution.Push(otherSolution.ParentAction.ShortToString());
                    //     otherSolution = otherSolution.Parent;
                    // }

                    // string normalBFSSolutionInverse = "The solution found by the normal BFS is composed by the following actions:\n";
                    // while (normalBFSSolution.Count > 0)
                    //     normalBFSSolutionInverse += normalBFSSolution.Pop() + "\n";

                    // writer.WriteLine(normalBFSSolutionInverse);

                    // // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

                    // roll back the simulation until we reach the root of the current level of detail
                    while (_currentNode.IsRoot == false)
                        _currentNode = _currentNode.Parent;

                    TreeNode<WorldState> lastNodeAtCurrentLevel = _lastObservedStates[currentSimulationBoundary];

                    int expandedNodes = 0;
                    long elapsedTimeForSearch = 0;


                    string s = "The solution found by the Subgoals BFS is composed by the following actions:\n";
                    // translate each action performed in the previous level of detail to an equivalent list of
                    // actions in the current level of detail. each translation is applied to the last observed
                    // state at current level of detail which in the end will be up to date.
                    while (_currentNode.IsLeaf == false)
                    {

                        // double desiredAccuracy = 1; // How accurate should be the translation 0 <= x <= 1
                        // int cutoff = 10;            // After how many levels we stop looking

                        _currentNode = _currentNode.Children.First();

                        var watch = System.Diagnostics.Stopwatch.StartNew();

                        TreeNode<WorldState> solution = Utils.breadthFirstSearch(lastNodeAtCurrentLevel.Data, _currentNode.Data);

                        watch.Stop();
                        long elapsedMs = watch.ElapsedMilliseconds;

                        expandedNodes += Utils.bfsExploredNodes;
                        elapsedTimeForSearch += elapsedMs;

                        // Debug.Log("BFS time: " + elapsedMs + " ms");
                        // Debug.Log("BFS explored nodes: " + Utils.bfsExploredNodes);

                        s += "Abstract: " + _currentNode.ParentAction.ShortToString() + "\n";

                        s += "Detailed:\n";

                        // solution is a leaf node we need to apply the actions in the reversed order (from root to leaf)
                        List<Action> sortedActions = new List<Action>();
                        while (solution.IsRoot == false)
                        {
                            sortedActions.Add(solution.ParentAction);
                            solution = solution.Parent;
                        }
                        sortedActions.Reverse();
                        foreach (Action a in sortedActions)
                        {
                            s += a.ShortToString() + "\n";
                            lastNodeAtCurrentLevel = lastNodeAtCurrentLevel.AddChild(lastNodeAtCurrentLevel.Data.applyAction(a), a);
                        }
                        s += "\n";
                    }

                    writer.WriteLine(s);

                    _currentNode = lastNodeAtCurrentLevel;

                    writer.WriteLine("Subgoals BFS search time: " + elapsedTimeForSearch + " ms");
                    writer.WriteLine("Subgoals BFS search explored nodes: " + expandedNodes);

                    writer.Close();

                }
                // Abstraction
                else if (_currentLevelOfDetail < lastLoD)
                {
                    if (currentSimulationBoundary != null)
                    {
                        WorldState worldStateAbstract = parseToLevelOfDetail(_currentNode.Data, currentSimulationBoundary.domain);

                        // start a new path the old history is kept in lastObservedStates
                        _currentNode = new TreeNode<WorldState>(worldStateAbstract);
                    }
                    else
                        Debug.LogError("No domain in simulationBoundaries corresponds to this LoD");
                }
                lastLoD = _currentLevelOfDetail;
            }

            // this is the actual simulation, for now we just pick a random action
            // remember to use lastLoD while updating the lastObservedState because
            // in the meantime it could have changed
            List<Action> parallelRandomActions = getRandomPossibleAction(_currentNode);

            if (parallelRandomActions == null || parallelRandomActions.Count <= 0)
            {
                Debug.Log("There are no more available actions, shutting down the simulation");
                break;
            }

            string chosenCombination = transform.parent.gameObject.name + "\n";
            foreach (Action a in parallelRandomActions)
                chosenCombination += a.ShortToString() + ",";
            Debug.Log(chosenCombination);

            // TODO: now we have many actions returned, we must apply all of them at once since, each one of them
            // involves a different active actor, so they can be displayed in parallel

            Action randomSuperAction;
            HashSet<IRelation> randomSuperActionPreconditions = new HashSet<IRelation>();
            string randomSuperActionName = "";
            HashSet<ActionParameter> randomSuperActionParameters = new HashSet<ActionParameter>();
            HashSet<IRelation> randomSuperActionPostconditions = new HashSet<IRelation>();

            foreach (Action randomAction in parallelRandomActions)
            {
                randomSuperActionPreconditions.UnionWith(randomAction.PreConditions);
                randomSuperActionName += randomAction.Name;
                randomSuperActionParameters.UnionWith(randomAction.Parameters);
                randomSuperActionPostconditions.UnionWith(randomAction.PostConditions);
            }

            randomSuperAction = new Action(randomSuperActionPreconditions, randomSuperActionName,
                randomSuperActionParameters, randomSuperActionPostconditions);


            // Debug.Log("The Simulator is requesting the following Action: " + randomAction.ShortToString());

            bool simulationInteractive = getSimulationBoundaryAtLevel(lastLoD).interactive;
            if (simulationInteractive)
            {
                // Debug.Log("Player is interacting");

                bool result = false;
                yield return StartCoroutine(visualizer.interact(randomSuperAction, value => result = value));
                if (result)
                {
                    // The action has been allowed, go next
                    // Debug.Log("Interactive Action Allowed");
                    WorldState resultingState = _currentNode.Data.applyAction(randomSuperAction);
                    _currentNode = _currentNode.AddChild(resultingState, randomSuperAction);

                    setLastObservedStateAtLevel(lastLoD, _currentNode);
                }
                else
                {
                    // The action has been denied, roll back
                    // Debug.Log("Interactive Action Denied");
                }
            }
            else
            {
                // Debug.Log("Player is visualizing");

                bool result = false;
                yield return StartCoroutine(visualizer.visualize(randomSuperAction, value => result = value));
                lastActionPerformed = randomSuperAction;
                if (result)
                {
                    // The action has been visualized, go next
                    // Debug.Log("Non Interactive Action Visualized");
                    WorldState resultingState = _currentNode.Data.applyAction(randomSuperAction);
                    _currentNode = _currentNode.AddChild(resultingState, randomSuperAction);

                    setLastObservedStateAtLevel(lastLoD, _currentNode);
                }
                else
                {
                    // The were some problems with the visualization, roll back
                    // Debug.Log("Non Interactive Action NOT Visualized");
                }
            }

            yield return null;
        }
    }

    private List<Action> getRandomPossibleAction(TreeNode<WorldState> node)
    {
        List<Action> possibleActions = node.Data.getPossibleActions();

        if (possibleActions.Count <= 0)
            return null;

        // we want to get one possible action for each different entity that can perform an action
        // so we need a way to group actions depending on the entity which is performing it
        Dictionary<ActionParameter, List<Action>> actionsForEachActor = new Dictionary<ActionParameter, List<Action>>();

        foreach (Action a in possibleActions)
        {
            foreach (ActionParameter ap in a.Parameters)
            {
                if (ap.Role == ActionParameterRole.ACTIVE)
                {

                    List<Action> actions;
                    if (!actionsForEachActor.TryGetValue(ap, out actions))
                    {
                        actions = new List<Action>();
                        actionsForEachActor.Add(ap, actions);
                    }
                    actions.Add(a);
                }
            }
        }

        // now that we have a list of possible actions for each active actor we want to build
        // a n-tuple containing one for each of them. The tuple must be composed by actions
        // which are applicable at the same time
        List<List<Action>> parallelActions = actionsForEachActor.Values.ToList().CartesianProduct();
        // foreach (List<Action> list in parallelActions)
        // {
        //     string s = "";
        //     foreach (Action a in list)
        //     {
        //         s += a.ShortToString() + "\n";
        //     }
        //     Debug.Log(s);
        // }

        // for each combinations we must check if the actions can really be 
        // carried out in parallel, meaning that we can perform them in 
        // whichever order we prefer, and still reach a consistent worldstate    
        List<Action> chosenRandomParallelActions = new List<Action>();
        List<int> randomIndexes = new List<int>(Enumerable.Range(0, parallelActions.Count).ToArray());

        // we choose a random order to explore the possible parallel actions
        randomIndexes.Shuffle();

        // we exit from this loop as soon as we find a set of parallel actions which is fully applicable
        // or when we have checked all the possible parallel actions and we did not find any 
        foreach (int i in randomIndexes)
        {
            List<Action> randomParallelActions = parallelActions[i];
            IEnumerable<IEnumerable<Action>> permutations = randomParallelActions.Permute();

            bool canPerfomParallelActions = true;

            // we exit from this loop when we check all the permutations
            // or when one of the permutations cannot be performed
            foreach (IEnumerable<Action> perm in permutations)
            {
                WorldState currentWorldState = _currentNode.Data.Clone();

                // we exit from this loop when we check all the actions in the
                // current permutation or when one action cannot be applied
                // because one of the previous actions modified the state, in a way
                // that does not satisfy the preconditions of the followin action
                foreach (Action a in perm)
                {
                    // we try to apply the action, if we get an exception
                    // it means that the action was not applicable
                    try
                    {
                        currentWorldState = currentWorldState.applyAction(a);
                    }
                    catch (System.ArgumentException)
                    {
                        canPerfomParallelActions = false;
                        break;
                    };
                }
                if (canPerfomParallelActions == false) break;
            }

            // if at this point this is still true, it means that the list
            // of actions can be carried out in whichever order possible
            // so we conclude that it can be carried out in parallel and we return it
            if (canPerfomParallelActions == true)
            {
                chosenRandomParallelActions = randomParallelActions;
                break;
            }
        }


        // int randomActionIndex = Random.Range(0, possibleActions.Count);
        // Action randomAction = possibleActions[randomActionIndex];

        // return randomAction;
        return chosenRandomParallelActions;
    }

    private TreeNode<WorldState> getInitialWorldStateAtLevel(int level)
    {
        WorldState worldStateAtLevel;
        switch (level)
        {
            case 1:
                Domain domainAbstract = Utils.roverWorldDomainFirstLevel();
                worldStateAtLevel = Utils.roverWorldStateFirstLevel(domainAbstract);
                break;
            case 2:
                Domain domainFullDetail = Utils.roverWorldDomainThirdLevel();
                worldStateAtLevel = Utils.roverWorldStateThirdLevel(domainFullDetail);
                break;
            default:
                Domain defaultDomain = Utils.roverWorldDomainFirstLevel();
                worldStateAtLevel = Utils.roverWorldStateFirstLevel(defaultDomain);
                break;
        }
        return new TreeNode<WorldState>(worldStateAtLevel);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
