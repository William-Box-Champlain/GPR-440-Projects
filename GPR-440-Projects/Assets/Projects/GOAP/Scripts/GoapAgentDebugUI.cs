using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GOAP
{
    /// <summary>
    /// Displays a UI element that shows the GOAP agent's current action, beliefs, and goal.
    /// Uses a line renderer to connect the UI to the agent.
    /// </summary>
    [RequireComponent(typeof(GoapAgent))]
    public class GoapAgentDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private Text goalText;
        [SerializeField] private Text actionText;
        [SerializeField] private Text beliefsText;
        [SerializeField] private RectTransform uiPanel;

        [Header("UI Settings")]
        [SerializeField] private Vector2 uiPosition = new Vector2(20, 20);
        [SerializeField] private int maxBeliefsToShow = 8;
        [SerializeField] private Color trueBeliefColor = Color.green;
        [SerializeField] private Color falseBeliefColor = Color.red;

        [Header("Line Settings")]
        [SerializeField] private Color lineColor = Color.white;
        [SerializeField] private float lineWidth = 1f;

        // References
        private GoapAgent agent;
        private Camera mainCamera;
        private LineRenderer lineRenderer;
        private GameObject lineObject;

        private void Awake()
        {
            agent = GetComponent<GoapAgent>();
            mainCamera = Camera.main;

            // Create UI if not assigned
            if (uiCanvas == null)
            {
                CreateUIElements();
            }
            
            // Create line renderer
            CreateLineRenderer();
        }

        private void CreateLineRenderer()
        {
            // Create a new GameObject for the line renderer
            lineObject = new GameObject("AgentUIConnector");
            lineObject.transform.SetParent(transform);
            
            // Add and configure the line renderer
            lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
        }

        private void CreateUIElements()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("GoapAgentUI_Canvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create panel
            GameObject panelObj = new GameObject("DebugPanel");
            panelObj.transform.SetParent(uiCanvas.transform, false);
            uiPanel = panelObj.AddComponent<RectTransform>();
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);
            
            // Set panel size and position
            uiPanel.sizeDelta = new Vector2(250, 200);
            uiPanel.anchorMin = new Vector2(0, 0);
            uiPanel.anchorMax = new Vector2(0, 0);
            uiPanel.pivot = new Vector2(0.5f, 0);

            // Create text elements
            goalText = CreateTextElement("GoalText", uiPanel, new Vector2(0, 160), "Current Goal: None");
            actionText = CreateTextElement("ActionText", uiPanel, new Vector2(0, 120), "Current Action: None");
            beliefsText = CreateTextElement("BeliefsText", uiPanel, new Vector2(0, 60), "Beliefs:\nNone");

            // Set text alignment
            goalText.alignment = TextAnchor.UpperLeft;
            actionText.alignment = TextAnchor.UpperLeft;
            beliefsText.alignment = TextAnchor.UpperLeft;
        }

        private Text CreateTextElement(string name, RectTransform parent, Vector2 position, string defaultText)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(230, 80);
            rectTransform.anchoredPosition = position;
            rectTransform.anchorMin = new Vector2(0.5f, 0);
            rectTransform.anchorMax = new Vector2(0.5f, 0);
            
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.text = defaultText;
            text.raycastTarget = false;
            
            return text;
        }

        private void Update()
        {
            UpdateUIPosition();
            UpdateUIContent();
            UpdateLineRenderer();
        }

        private void UpdateUIPosition()
        {
            if (uiPanel == null) return;

            // Set UI to fixed position
            uiPanel.position = new Vector3(uiPosition.x + uiPanel.sizeDelta.x / 2, uiPosition.y + uiPanel.sizeDelta.y / 2, 0);
        }

        private void UpdateLineRenderer()
        {
            if (lineRenderer == null || mainCamera == null) return;

            // Update line renderer properties
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            // Set the start point (agent position)
            lineRenderer.SetPosition(0, transform.position);

            // Set the end point (UI panel position in world space)
            // Convert UI position from screen space to world space
            Vector3 uiScreenPos = uiPanel.position;
            uiScreenPos.z = 10; // Set a z-distance from the camera
            Vector3 uiWorldPos = mainCamera.ScreenToWorldPoint(uiScreenPos);
            
            lineRenderer.SetPosition(1, uiWorldPos);
        }

        private void UpdateUIContent()
        {
            if (agent == null) return;

            // Update goal text
            goalText.text = $"Current Goal: {(agent.CurrentGoal != null ? agent.CurrentGoal.Name : "None")}";
            
            // Update action text
            actionText.text = $"Current Action: {(agent.currentAction != null ? agent.currentAction.Name : "None")}";
            
            // Update beliefs text
            if (agent.beliefs != null && agent.beliefs.Count > 0)
            {
                StringBuilder sb = new StringBuilder("Beliefs:\n");
                
                // Get the most relevant beliefs (true ones first, then limited by maxBeliefsToShow)
                var sortedBeliefs = agent.beliefs
                    .OrderByDescending(b => b.Value.Evaluate())
                    .Take(maxBeliefsToShow)
                    .ToList();
                
                foreach (var belief in sortedBeliefs)
                {
                    bool isTrue = belief.Value.Evaluate();
                    string colorTag = isTrue 
                        ? ColorUtility.ToHtmlStringRGB(trueBeliefColor) 
                        : ColorUtility.ToHtmlStringRGB(falseBeliefColor);
                    
                    sb.AppendLine($"- <color=#{colorTag}>{belief.Key}: {isTrue}</color>");
                }
                
                beliefsText.text = sb.ToString();
            }
            else
            {
                beliefsText.text = "Beliefs:\nNone";
            }
        }
    }
}
