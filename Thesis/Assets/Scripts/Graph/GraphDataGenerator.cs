﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ru.cadia.pddlFramework;

public class GraphDataGenerator
{
    private WorldState _startingState;
    private Graph _graph;
    private HashSet<WorldState> _visitedStates;
    private HashSet<WorldState> _finalStates;
    private HashSet<WorldStateComparated> _worldStateComparated;
    private System.Diagnostics.Stopwatch sw;
    private int numberOfLevels;

    public GraphDataGenerator(WorldState ws)
    {
        _startingState = ws.Clone();
        _graph = new Graph();
        _visitedStates = new HashSet<WorldState>();
        _finalStates = new HashSet<WorldState>();
        _worldStateComparated = new HashSet<WorldStateComparated>();
        sw = new System.Diagnostics.Stopwatch();
    }

    public Graph GenerateData(int level)
    {
        numberOfLevels = level;
        _graph.AddNode(_startingState, 1);
        Debug.Log("Starting Generation");
        sw.Start();
        GenerateDataRoutine(_startingState, 2);
        sw.Stop();
        Debug.Log("Data Generation time:" + (sw.ElapsedMilliseconds / 1000f));
        return _graph;
    }
    private void GenerateDataRoutine(WorldState currentState, int level)
    {
        List<Action> possibleActions = currentState.getPossibleActions();
        foreach (Action item in possibleActions)
        {
            WorldState ws = currentState.applyAction(item);
            _graph.addEdge(currentState, ws, item, level);
            if (!_visitedStates.Contains(ws))
            {
                _visitedStates.Add(ws.Clone());
                if (level + 1 > numberOfLevels)
                {
                    _finalStates.Add(ws.Clone());
                }
                else
                {
                    GenerateDataRoutine(ws, level + 1);
                }
            }
            else
            {
                if (level + 1 > numberOfLevels)
                {
                    _finalStates.Add(ws.Clone());
                }
            }
        }
    }

    public HashSet<WorldStateComparated> CompareWorldState()
    {
        foreach (WorldState item in _finalStates)
        {
            WorldStateComparated wsc = new WorldStateComparated(_startingState, item);
            wsc.CompareStates();
            _worldStateComparated.Add(wsc);
            // Debug.Log(wsc);
        }

        return _worldStateComparated;
    }
}
