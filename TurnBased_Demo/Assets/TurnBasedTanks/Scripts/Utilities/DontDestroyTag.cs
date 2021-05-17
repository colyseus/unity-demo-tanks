using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DontDestroyTag : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
