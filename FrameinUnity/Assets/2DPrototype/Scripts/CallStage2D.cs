using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class CallStage2D : MonoBehaviour
{

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SceneManager.LoadScene("Stage2D");
        }
    }
}