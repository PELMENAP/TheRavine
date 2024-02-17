using UnityEngine;
using UnityEngine.InputSystem;

using TheRavine.Base;
using TheRavine.Services;
public class CM : MonoBehaviour, ISetAble
{
    [SerializeField] private Transform cameratrans, playerTrans;
    [SerializeField] private float Velocity, MinDistance;
    public Camera mainCam;
    public static bool cameraForMap;
    private Vector3 offset, targetPos;
    // private bool changeCam = false;
    PlayerEntity PData;
    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        PData = locator.GetService<PlayerEntity>();
        // cameraForMap = false;
        playerTrans = PData.transform;
        offset = cameratrans.position - playerTrans.position;
        cameratrans.position = playerTrans.position + new Vector3(0, 0, -1);
        callback?.Invoke();
    }

    public void CameraUpdate()
    {
        // if ((changeCam || (Input.GetKeyUp("p")) && Input.GetKeyDown(KeyCode.LeftControl)))
        // if (false)
        // {
        //     if (cameraForMap)
        //     {
        //         cameratrans.position = playerTrans.position + new Vector3(0, 0, -1);
        //         mainCam.orthographicSize = 20;
        //     }
        //     else
        //     {
        //         cameratrans.position += new Vector3(0, 0, 99);
        //         mainCam.orthographicSize = 100;
        //     }
        //     cameraForMap = !cameraForMap;
        //     changeCam = false;
        // }
        // if (cameraForMap)
        // {
        //     UpdateForMap();
        // }
        // else
        // {
        UpdateForGame();
        // }
    }

    // public void Changed()
    // {
    //     changeCam = true;
    // }

    private void UpdateForGame()
    {
        targetPos = playerTrans.position + offset;
        switch (Settings._controlType)
        {
            case ControlType.Personal:
                if (Mouse.current.rightButton.isPressed)
                    targetPos += PData.factMousePosition;
                break;
            case ControlType.Mobile:
                targetPos += PData.factMousePosition;
                break;
        }
        if (Vector3.Distance(cameratrans.position, targetPos) < MinDistance)
            return;
        cameratrans.Translate(cameratrans.InverseTransformPoint(Vector3.Lerp(cameratrans.position, targetPos, Velocity * Time.fixedDeltaTime)));
    }

    private void UpdateForMap()
    {
        if (Input.GetKey("["))
        {
            mainCam.orthographicSize -= 20;
        }
        else if (Input.GetKey("]"))
        {
            mainCam.orthographicSize += 20;
        }
        else if (Input.mouseScrollDelta.y != 0)
        {
            mainCam.orthographicSize += Input.mouseScrollDelta.y * 20 * mainCam.orthographicSize / 300;
            this.transform.Translate(new Vector3((Input.mousePosition.x - Screen.width / 2) * 0.5f, (Input.mousePosition.y - Screen.height / 2) * 0.5f, 0) * (Input.mouseScrollDelta.y > 0 ? -1 : 1) * mainCam.orthographicSize / 200);
        }
        if (mainCam.orthographicSize > 1000)
        {
            mainCam.orthographicSize = 1000;
        }
        else if (mainCam.orthographicSize < 10)
        {
            mainCam.orthographicSize = 10;
        }
    }

    public void BreakUp()
    {

    }
}