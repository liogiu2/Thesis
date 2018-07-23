﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ru.cadia.pddlFramework;
public class Visualization : MonoBehaviour
{

    public Text displayText;

    private float interactionWaitTime;
    private float visualizationWaitTime;
    private float interactionSuccessProbability;
    private float visualizationSuccessProbability;


    // Use this for initialization
    void Start()
    {
        interactionWaitTime = 3.0f;
        visualizationWaitTime = 3.0f;

        interactionSuccessProbability = 0.7f;
        visualizationSuccessProbability = 0.8f;

        // displayText.transform.position = transform.position;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public IEnumerator interact(Action a, System.Action<bool> result)
    {
        displayText.text = "The Simulator is requesting the following Action: " + a.ShortToString();

        yield return new WaitForSeconds(interactionWaitTime);

        // TODO: the logic to interact with the action should go here

        float outcome = Random.Range(0.0f, 1.0f);
        if (outcome <= interactionSuccessProbability)
        {
            displayText.text = "Interactive Action Allowed";
            yield return new WaitForSeconds(1.0f);
            result(true);
        }
        else
        {
            displayText.text = "Interactive Action Denied";
            yield return new WaitForSeconds(1.0f);
            result(false);
        }
    }

    public IEnumerator visualize(Action a, System.Action<bool> result)
    {
        displayText.text = "The Simulator is requesting the following Action: " + a.ShortToString();

        yield return new WaitForSeconds(visualizationWaitTime);

        // TODO: the logic to visualize the action should go here

        float outcome = Random.Range(0.0f, 1.0f);
        if (outcome <= visualizationSuccessProbability)
        {
            displayText.text = "Non Interactive Action Visualized";
            yield return new WaitForSeconds(1.0f);
            result(true);
        }
        else
        {
            displayText.text = "Non Interactive NOT Action Visualized";
            yield return new WaitForSeconds(1.0f);
            result(false);
        }
    }
}
