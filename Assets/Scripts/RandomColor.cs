using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomColor : MonoBehaviour
{
    float min = 0.1f;
    float max = 1f;
    void Start()
    {
        GetComponent<Renderer>().material.color = Random.ColorHSV(0f, 1f, min, max, min, max);
    }
}
