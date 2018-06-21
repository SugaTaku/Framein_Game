using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class CallMain2D : MonoBehaviour
{

    // Use this for initialization
    IEnumerator Start()
    {
        yield return new WaitForSeconds(2);
        SceneManager.LoadScene("Main2D");
    }
}