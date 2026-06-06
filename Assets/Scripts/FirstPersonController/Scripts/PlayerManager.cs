using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.EventSystems;

/// <summary>
/// This class will enable the touch input canvas on handheld devices and will trigger the camera flythrough if the player is idle
/// </summary>
public class PlayerManager : MonoBehaviour
{
    [SerializeField] private bool m_FlythroughWhenIdle;
    [SerializeField] private float m_IdleTransitionTime;
    [SerializeField] private GameObject m_CrosshairCanvas;
    [SerializeField] private GameObject m_TouchInputCanvas;
    [SerializeField] private GameObject m_EventSystem;

    private bool m_InFlythrough;
    private float m_TimeIdle;
    private Camera m_VirtualCamera;
    private bool m_HasFocus;
    private bool m_IsMobilePlatform;

    void Start()
    {
        if (EventSystem.current == null)
        {
            m_EventSystem.SetActive(true);
        }

        m_InFlythrough = false;
        
        m_IsMobilePlatform = Application.isMobilePlatform;
        SetTouchInputCanvasActive(true);

        m_VirtualCamera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (m_FlythroughWhenIdle && m_TimeIdle > m_IdleTransitionTime && !m_InFlythrough)
        {
            m_TimeIdle = 0;
            EnableFlythrough();
        }
        
        #if UNITY_EDITOR
        if(m_HasFocus) m_TimeIdle += Time.unscaledDeltaTime;
        #else
        m_TimeIdle += Time.unscaledDeltaTime;
        #endif
    }

#if ZERO
    private void Awake()
    {
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }
#endif // ZERO

    public void EnableFlythrough()
    {
        m_InFlythrough = true;
        m_CrosshairCanvas.SetActive(false);
        SetTouchInputCanvasActive(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        m_HasFocus = hasFocus;
    }

    public void EnableFirstPersonController()
    {
        m_CrosshairCanvas.SetActive(true);
        m_InFlythrough = false;
    }

    public void NotifyPlayerMoved()
    {
        m_TimeIdle = 0;
        if (m_InFlythrough)
        {
            EnableFirstPersonController();
            SetTouchInputCanvasActive(true);
        }
    }
    
    private void SetTouchInputCanvasActive(bool enable)
    {
        if (m_IsMobilePlatform)
        {
            m_TouchInputCanvas.SetActive(enable);
        }
    }
}
