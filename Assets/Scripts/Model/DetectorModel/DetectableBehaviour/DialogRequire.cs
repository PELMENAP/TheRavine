using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System;
using TMPro;

[RequireComponent(typeof(Detector))]
public class DialogRequire : MonoBehaviour
{
    // [SerializeField] private BotController Controller;
    [SerializeField] private TextMeshPro text;
    [SerializeField] private string[] lines;
    [SerializeField] private float textSpeed;
    [SerializeField] private int stayOnGUI, silentOnGUI;
    [SerializeField] private GameObject signature, mark;
    [SerializeField] private bool bot, setName, mayStartDialog;
    [SerializeField] private TextMeshPro hame;
    [SerializeField] private string[] names;
    [SerializeField] private Conversation[] conversations;
    [SerializeField] private string[] dontUnderstand;
    [SerializeField] private string[] endDialog;
    private Transform intObj;
    private int startSilent;
    private bool stayDetector, Dialog, dialogComplete;
    private IDetector _idetector;

    private string playerText;

    private void Awake()
    {
        _idetector = (Detector)this.GetComponent("Detector");
        _idetector.OnGameObjectDetectedEvent += OnDetectedEvent;
        _idetector.OnGameObjectDetectionReleasedEvent += OnDetectionReleasedEvent;
        intObj = GameObject.Find("InteractiveObject").transform;
        if (setName)
            hame.text = names[UnityEngine.Random.Range(0, names.Length)];
        Dialog = false;
        dialogComplete = true;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (stayDetector)
            return;
        if (source.CompareTag("Player"))
        {
            stayDetector = true;
            StartCoroutine(DetectionController(source, detectedObject));
            if (signature != null)
                signature.SetActive(true);
            if (mark != null)
            {
                mark.transform.parent = intObj;
                mark.SetActive(true);
                mark = null;
            }
        }
    }

    private void OnDetectionReleasedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player"))
        {
            stayDetector = false;
            if (signature != null)
                signature.SetActive(false);
            Dialog = false;
            if (bot)
            {
                // Controller.SetBehaviourIdle();
            }
        }
    }

    private IEnumerator DetectionController(GameObject source, GameObject detectedObject)
    {
        do
        {
            yield return new WaitForSeconds(1f);
            // if (TimeUpdate.globalTimeInt - startSilent > silentOnGUI && !Dialog)
            //     StartCoroutine(TypeLineIdle());
            if ((Input.GetKey("0") || Input.GetKey("q")) && mayStartDialog)
            {
                if (!Dialog && dialogComplete)
                    DialogStart();
                if (bot)
                {
                    // Controller.SetBehaviourDialog();
                    // Controller.SetSpeed();
                }
            }
        }
        while (stayDetector);
    }

    private IEnumerator TypeLineIdle()
    {
        // startSilent = TimeUpdate.globalTimeInt;
        text.text = "";
        foreach (char i in lines[UnityEngine.Random.Range(0, lines.Length)].ToCharArray())
        {
            if (Dialog)
            {
                text.text = "";
                yield break;
            }
            //continue;
            text.text += i;
            yield return new WaitForSeconds(textSpeed);
        }
        yield return new WaitForSeconds(stayOnGUI);
        if (!Dialog)
            text.text = "";
    }

    private async void DialogStart()
    {
        dialogComplete = false;
        Dialog = true;
        do
            await DialogProcess();
        while (Dialog);
        // PlayerController.instance.dialog.SetActive(false);
        dialogComplete = true;
    }

    private async Task DialogProcess()
    {
        // PlayerController.instance.dialog.SetActive(true);
        // while ((!Input.GetKey(KeyCode.Return) || PlayerController.instance.InputWindow.text.Length > 50) && Dialog) await Task.Delay(100);
        if (!Dialog)
            return;
        // PlayerController.instance.SetBehaviourDialog();
        // PlayerController.instance.dialog.SetActive(false);
        // playerText = PlayerController.instance.InputWindow.text.Remove(PlayerController.instance.InputWindow.text.Length - 1);
        // StartCoroutine(PlayerDialogControoller.instance.TypeLine(playerText));
        if (endDialog.Contains(playerText))
        {
            Dialog = false;
            if (bot)
            {
                // Controller.SetBehaviourIdle();
            }
        }
        bool niceDialog = false;
        for (int i = 1; i < conversations.Length; i++)
        {
            try
            {
                if (!niceDialog)
                {
                    for (int j = 0; j < conversations[i].Speech[conversations[i].step].comment.Length; j++)
                    {
                        if (String.Compare(playerText, conversations[i].Speech[conversations[i].step].comment[j], StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            StartCoroutine(TypeLineCompanion(conversations[i].Answer[conversations[i].step].comment[UnityEngine.Random.Range(0, conversations[i].Answer[conversations[i].step].comment.Length)]));
                            conversations[i].step++;
                            // if (conversations[i].step >= conversations[i].Speech.Length && PData.pdata.taskManager != null && conversations[i].TaskID != 0)
                            // {
                            //     PData.pdata.taskManager.ActivateTask(conversations[i].TaskID, this.transform.position);
                            // }
                            niceDialog = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
            }
        }
        if (!niceDialog && Dialog)
            StartCoroutine(TypeLineCompanion(dontUnderstand[UnityEngine.Random.Range(0, dontUnderstand.Length)]));
        if (Dialog)
            await Task.Delay(9000);
        else
            await Task.Delay(1000);
        // PlayerController.instance.SetBehaviourIdle();
    }
    private IEnumerator TypeLineCompanion(string speech)
    {
        yield return new WaitForSeconds(3f);
        text.text = "";
        foreach (char i in speech.ToCharArray())
        {
            if (!Dialog)
            {
                text.text = "";
                yield break;
            }
            text.text += i;
            yield return new WaitForSeconds(0.07f);
        }
        yield return new WaitForSeconds(3f);
        if (Dialog)
            text.text = "";
    }

    [System.Serializable]
    public struct Conversation
    {
        public Dictum[] Speech;
        public Dictum[] Answer;
        public int step;
        public int TaskID;
    }

    [System.Serializable]
    public struct Dictum
    {
        public string[] comment;
    }
}
