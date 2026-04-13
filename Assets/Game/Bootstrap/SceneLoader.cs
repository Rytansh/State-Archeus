using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadBattleScene()
    {
        SceneManager.UnloadSceneAsync("MenuScene");
        SceneManager.LoadScene("BattleScene", LoadSceneMode.Additive);
    }
}
