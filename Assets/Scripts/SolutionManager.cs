using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class SolutionManager : MonoBehaviour
{
    public UIDocument uiDocument;
    private Label problemLabel;
    private Label stepLabel;
    private Button nextButton;
    private Button previousButton;

    private DiagnosisResponse currentFix;
    private int currentStepIndex = 0;

    void Start()
    {
        VisualElement root = uiDocument.rootVisualElement;
        problemLabel = root.Q<Label>("problem-label");
        stepLabel = root.Q<Label>("step-label");
        nextButton = root.Q<Button>("next-button");
        previousButton = root.Q<Button>("previous-button");
        nextButton.clicked += OnNextStepPressed;
        previousButton.clicked += OnPreviousStepPressed;
        currentFix = DiagnosisResponse.LastDiagnosis;

        if (currentFix == null)
        {
            Debug.LogError("No diagnosis data found! Going back.");
            SceneManager.LoadScene("HomeUploadScene");
            return;
        }
        problemLabel.text = "Problem: " + currentFix.problem;
        ShowStep(currentStepIndex);
    }

    void OnNextStepPressed()
    {
        currentStepIndex++;
        ShowStep(currentStepIndex);
    }
    void OnPreviousStepPressed()
    {
        currentStepIndex--;
        ShowStep(currentStepIndex);
    }

    void ShowStep(int index)
    {
        previousButton.style.display = (index == 0) ? DisplayStyle.None : DisplayStyle.Flex;
        if (index >= currentFix.steps.Length)
        {
            stepLabel.text = "Repair complete! Well done.";
            nextButton.text = "Finish";
            nextButton.clicked -= OnNextStepPressed;
            nextButton.clicked += () => { SceneManager.LoadScene("HomeUploadScene"); };
        }
        else
        {
            nextButton.text = "Next Step";
            nextButton.clicked -= OnNextStepPressed; 
            nextButton.clicked += OnNextStepPressed; 
            stepLabel.text = currentFix.steps[index].text;
            Debug.Log("AR Model to show: " + currentFix.steps[index].ar_model);
        }
    }
}