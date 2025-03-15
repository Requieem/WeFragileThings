using Fusion;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Security.Cryptography;
using TMPro;
using Button = UnityEngine.UI.Button;

public class NetworkMenu : MonoBehaviour
{
    public class SessionNameGenerator
    {
        private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        
        public static string GenerateSessionName()
        {
            char[] result = new char[6];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[6];
                rng.GetBytes(randomBytes);

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Characters[randomBytes[i] % Characters.Length];
                }
            }

            return new string(result);
        }
    }

    [SerializeField] private GameObject m_joinPanel;
    [SerializeField] private Button m_joinRandomButton;
    [SerializeField] private Button m_joinNamedButton;
    [SerializeField] private Button m_createNamedButton;
    [SerializeField] private TMP_InputField m_sessionNameField;
    [SerializeField] private TextMeshProUGUI m_maxPlayersText;
    [SerializeField] private TextMeshProUGUI m_sessionNameText;
    [SerializeField] private TextMeshProUGUI m_errorText;
    [SerializeField] private Slider m_maxPlayersSlider;
    [SerializeField] private NetworkRunner m_runnerPrefab;
    
    private NetworkRunner m_runnerInstance;

    private void OnEnable()
    {
        if (m_runnerPrefab != null)
        {
            m_runnerInstance = Instantiate(m_runnerPrefab);
        }
        
        m_joinRandomButton.onClick.AddListener(JoinRandom);
        m_joinNamedButton.onClick.AddListener(JoinNamed);
        m_createNamedButton.onClick.AddListener(CreateNamed);
        m_maxPlayersSlider.onValueChanged.AddListener(UpdateMaxPlayersText);
    }

    private void OnDisable()
    {
        m_joinRandomButton.onClick.RemoveListener(JoinRandom);
        m_joinNamedButton.onClick.RemoveListener(JoinNamed);
        m_createNamedButton.onClick.RemoveListener(CreateNamed);
        m_maxPlayersSlider.onValueChanged.RemoveListener(UpdateMaxPlayersText);
    }
    
    private void UpdateMaxPlayersText(float value)
    {
        m_maxPlayersText.text = $"{value:N0}";
    }
    
    private async void JoinRandom()
    {
        try
        {
            await JoinRandom(m_runnerInstance);
        } catch
        {
            Debug.Log("There was an error when trying to connect");
        }
    }
    
    private async void JoinNamed()
    {
        try
        {
            await JoinNamed(m_runnerInstance, m_sessionNameField.text);
        } catch
        {
            Debug.Log("There was an error when trying to connect");
        }
    }
    
    private async void CreateNamed()
    {
        try
        {
            await CreateNamed(m_runnerInstance, (int)m_maxPlayersSlider.value);
        } catch
        {
            Debug.Log("There was an error when trying to connect");
        }
    }

    private void Joined(NetworkRunner runner)
    {
        m_joinPanel.SetActive(false);
        m_sessionNameText.gameObject.SetActive(true);
        m_sessionNameText.text = $"Room ID: {runner.SessionInfo.Name}";
    }

    private void Left()
    {
        m_joinPanel.SetActive(true);
        m_sessionNameText.gameObject.SetActive(false);
    }

    private void SetError(string error = null)
    {
        if(error == null)
        {
            m_errorText.gameObject.SetActive(false);
            return;
        }
        m_errorText.text = error;
    }

    private async Task JoinRandom(NetworkRunner runner)
    {
        if (runner == null)
        {
            Debug.LogError("NetworkRunner is null!");
            return;
        }

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared, // Use Shared mode instead of Client/Host
        });

        if (!result.Ok)
        {
            Debug.LogError($"Failed to join a random session: {result.ShutdownReason}");
            SetError(result.ErrorMessage);
        }
        else
        {
            Joined(runner);
            SetError(null);
        }
    }

    private async Task JoinNamed(NetworkRunner runner, string sessionName)
    {
        if (runner == null)
        {
            Debug.LogError("NetworkRunner is null!");
            return;
        }

        var result = await runner.StartGame(new StartGameArgs
        {
            SessionName = sessionName,
            GameMode = GameMode.Shared, // Use Shared mode for named sessions
        });

        if (!result.Ok)
        {
            Debug.LogError($"Failed to join session '{sessionName}': {result.ShutdownReason}");
            SetError(result.ErrorMessage);
        }
        else
        {
            Joined(runner);
            SetError(null);
        }
    }

    private async Task CreateNamed(NetworkRunner runner, int maxPlayers)
    {
        if (runner == null)
        {
            Debug.LogError("NetworkRunner is null!");
            return;
        }

        const int maxRetries = 5;
        int attempts = 0;
        ShutdownReason error;
        StartGameResult result;

        do
        {
            result = await runner.StartGame(new StartGameArgs
            {
                SessionName = SessionNameGenerator.GenerateSessionName(),
                GameMode = GameMode.Shared, // Switch to Shared mode
                PlayerCount = Mathf.Clamp(maxPlayers, 1, 4),
            });

            error = result.ShutdownReason;
            attempts++;

            if (error == ShutdownReason.GameIdAlreadyExists)
            {
                Debug.LogWarning($"Session name collision. Retrying... ({attempts}/{maxRetries})");
            }

        } while (error == ShutdownReason.GameIdAlreadyExists && attempts < maxRetries);

        if (attempts == maxRetries)
        {
            Debug.LogError("Failed to create a session after multiple attempts.");
            SetError("Failed to create a session after multiple attempts.");
        }
        else if (result.Ok)
        {
            Joined(runner);
            SetError(null);
        }
        else
        {
            Debug.LogError($"Failed to create a session: {result.ShutdownReason}");
            SetError(result.ErrorMessage);
        }
    }
}