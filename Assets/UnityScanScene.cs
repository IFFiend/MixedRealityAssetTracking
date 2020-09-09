using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Input;
using System;

/// <summary>
/// The Interface gives one method to implement. This method modifies the text to display in Unity
/// Any code can then call this method outside the Unity MonoBehavior object
/// </summary>
public class NetworkOutput
{
    public NetworkOutput(string TimeStamp, string Asset, string Confidence, float x, float y, float z, float width, float height)
    {
        this.TimeStamp = TimeStamp;
        this.Asset = Asset;
        this.Confidence = Confidence;
        this.Coordinate = new Vector3(x, y, z);
        this.Width = width;
        this.Height = height;
    }

    public string TimeStamp { get; set; }
    public string Asset { get; set; }
    public string Confidence { get; set; }
    public Vector3 Coordinate { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

}
public interface IUnityScanScene
{
    void AddToMessageFlow(string message);
    void CreateObjectAtVector(NetworkOutput output);
    void StoreNetworkResult(string TimeStamp, string Asset, string Confidence, float x, float y, float z, float width, float height);
}

public class UnityScanScene : MonoBehaviour, IUnityScanScene
{
    // Unity 3D Text object that contains 
    // the displayed TextMesh in the FOV
    public GameObject OutputText;

    // Variable to control reset source point
    public bool SourcePointReset = false;
    // TextMesh object provided by the OutputText game object
    private TextMesh OutputTextMesh;
    // string to be affected to the TextMesh object
    private string NetworkResult = string.Empty;
    // Indicate if we have to Update the text displayed
    private bool OutputTextChanged = false;
    private List<string> AssetTypes = new List<string>() { "Vending Machine", "PlaceHolder1", "PlaceHolder2", "TEST" };
    private List<NetworkOutput> NetworkResults = new List<NetworkOutput>();
    private List<string> MessageFlow = new List<string>();
    private List<Vector3> ExistedCoordinates = new List<Vector3>();
    public Canvas canvas;
    // Use this for initialization
    public GameObject MainCamera;
    public GameObject TextButton;

    public GameObject SelectionButton;

    public GameObject Menu;

    async void Start()
    {
        OutputTextMesh = OutputText.GetComponent<TextMesh>();
        OutputTextMesh.text = string.Empty;
        StoreNetworkResult("0", "TEST", "100%", 0, 0, -1, 1.5f, 3);
        StoreNetworkResult("0", "TEST", "100%", 0, 0, -1, 1.5f, 3);
        // canvas = GameObject.FindGameObjectWithTag("MainCanva").GetComponent<Canvas>();
        Menu.transform.Find("ButtonCollection").Find("ButtonOne").gameObject.GetComponent<Interactable>().OnClick.AddListener(() => { StoreNetworkResult("0", "TEST", "100%", Menu.transform.position.x, Menu.transform.position.y, -Menu.transform.position.z); });
#if UNITY_WSA && !UNITY_EDITOR // **RUNNING ON WINDOWS**
        Debug.Log("Starting scan engine");
        var CameraScanEngine = new ScanEngine();   // <-- 1
        await CameraScanEngine.Inititalize(this);  // <-- 2
        CameraScanEngine.StartPullCameraFrames();  // <-- 3
        Menu.transform.Find("ButtonCollection").Find("ButtonTwo").gameObject.GetComponent<Interactable>().OnClick.AddListener(() => { CameraScanEngine.ResetReferenceFrame(); });
        Debug.Log("Scan engine started");
#else                          // **RUNNING IN UNITY**
        AddToMessageFlow("Sorry ;-( The app is not supported in the Unity player.");
#endif
    }

    public void StoreNetworkResult(string TimeStamp, string Asset, string Confidence, float x, float y, float z, float width = 0f, float height = 0f)
    {
        // Hololens uses inverse version of coordinate system where negative z means forward
        NetworkResults.Add(new NetworkOutput(TimeStamp, Asset, Confidence, x, y, -z, width, height));
    }

    List<Vector3> CalculateCoordinate(Vector3 baseCoordinate, bool smaller = true)
    {
        float buttonLocationInterval = 0.035f;
        float distanceBetween = 0.035f;
        if (smaller)
        {
            buttonLocationInterval = 0.023f;
            distanceBetween = 0.012f;
        }
        List<Vector3> vectors = new List<Vector3>();
        vectors.Add(new Vector3(-distanceBetween, buttonLocationInterval, 0));
        vectors.Add(new Vector3(0, buttonLocationInterval, 0));
        vectors.Add(new Vector3(distanceBetween, buttonLocationInterval, 0));
        return vectors;
    }

    GameObject CreateButton(Vector3 coordinate, string displayText, string iconName, GameObject parentButton, GameObject buttonType, string buttonTag, float scale)
    {
        GameObject button = Instantiate(buttonType);
        button.transform.parent = parentButton.transform;
        button.transform.localScale = new Vector3(scale, scale, scale);
        button.transform.localPosition = coordinate;
        button.transform.localRotation = Quaternion.identity;
        button.tag = buttonTag;
        ButtonConfigHelper configHelper = button.GetComponent<ButtonConfigHelper>();
        configHelper.MainLabelText = displayText;
        configHelper.SeeItSayItLabelEnabled = false;
        configHelper.SetQuadIconByName(iconName);
        return button;
    }

    void ObjectPressed(NetworkOutput output, GameObject parentButton)
    {
        bool buttonClicked = false;
        foreach (Transform child in parentButton.transform)
        {
            if (child.tag == "selectionButton")
            {
                Destroy(child.gameObject);
                buttonClicked = true;
            }
        }
        if (buttonClicked) return;
        List<Vector3> buttonCoordinates = CalculateCoordinate(output.Coordinate);
        GameObject buttonAccept = CreateButton(buttonCoordinates[0], "Accept", "IconDone", parentButton, SelectionButton, "selectionButton", 0.33f);
        GameObject buttonEdit = CreateButton(buttonCoordinates[1], "Edit", "IconSettings", parentButton, SelectionButton, "selectionButton", 0.33f);
        GameObject buttonReject = CreateButton(buttonCoordinates[2], "Reject", "IconClose", parentButton, SelectionButton, "selectionButton", 0.33f);

        buttonAccept.GetComponent<Interactable>().OnClick.AddListener(() => { AcceptPressed(output, parentButton); });
        buttonEdit.GetComponent<Interactable>().OnClick.AddListener(() => { EditPressed(output, parentButton, buttonEdit); });
        buttonReject.GetComponent<Interactable>().OnClick.AddListener(() => { RejectPressed(parentButton); });

    }

    void AcceptPressed(NetworkOutput output, GameObject parentButton)
    {
        AcceptColorChange(parentButton);
        if (parentButton.GetComponent<ManipulationHandler>() != null)
        {
            Destroy(parentButton.GetComponent<ManipulationHandler>());
            Destroy(parentButton.GetComponent<NearInteractionGrabbable>());
        }
        // upload to server
    }

    void AcceptColorChange(GameObject parentButton)
    {
        parentButton.transform.Find("BackPlate").Find("Quad").gameObject.GetComponent<Renderer>().material.color = Color.green;
    }

    void EditColorChange(GameObject parentButton)
    {
        parentButton.transform.Find("BackPlate").Find("Quad").gameObject.GetComponent<Renderer>().material.color = Color.blue;
    }

    void EditPressed(NetworkOutput output, GameObject parentButton, GameObject buttonEdit)
    {
        bool buttonClicked = false;
        foreach (Transform child in buttonEdit.transform)
        {
            if (child.tag == "EditPopUp")
            {
                Destroy(child.gameObject);
                buttonClicked = true;
            }
        }
        if (buttonClicked) return;

        List<Vector3> coordinates = CalculateCoordinate(buttonEdit.transform.position, false);
        GameObject TSButton = CreateButton(coordinates[0], output.TimeStamp, "IconAdjust", buttonEdit, TextButton, "EditPopUp", 1);
        GameObject AssetButton = CreateButton(coordinates[1], output.Asset, "IconAdjust", buttonEdit, TextButton, "EditPopUp", 1);
        GameObject CoordinateButton = CreateButton(coordinates[2], output.Coordinate.ToString(), "IconAdjust", buttonEdit, TextButton, "EditPopUp", 1);

        AssetButton.GetComponent<Interactable>().OnClick.AddListener(() => { AssetEditPopUpPressed(output, parentButton, AssetButton); });
        TSButton.GetComponent<Interactable>().OnClick.AddListener(() => { TimeStampEditPopUpPressed(output, parentButton, TSButton); });
        CoordinateButton.GetComponent<Interactable>().OnClick.AddListener(() => { CoordinateEditPopUpPressed(output, parentButton, CoordinateButton); });
    }
    void RejectPressed(GameObject asset)
    {
        Destroy(asset);
    }

    void AssetEditPopUpPressed(NetworkOutput output, GameObject parentButton, GameObject AssetButton)
    {
        int i = AssetTypes.IndexOf(output.Asset);
        i += 1;
        if (i >= AssetTypes.Count) i = 0;
        output.Asset = AssetTypes[i];
        EditColorChange(parentButton);
        AssetButton.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>().text = output.Asset;
        parentButton.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>().text = $"{output.TimeStamp}\n{output.Asset}\n{output.Confidence}\n{output.Coordinate.ToString()}";
    }
    void TimeStampEditPopUpPressed(NetworkOutput output, GameObject parentButton, GameObject TSButton)
    {
        output.TimeStamp = $"({DateTime.Now})";
        EditColorChange(parentButton);
        TSButton.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>().text = output.TimeStamp;
        parentButton.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>().text = $"{output.TimeStamp}\n{output.Asset}\n{output.Confidence}\n{output.Coordinate.ToString()}";
    }
    void updateCoordinate(NetworkOutput output, GameObject parentButton, GameObject CoordianteButton)
    {
        output.Coordinate = parentButton.transform.position;
        if (CoordianteButton != null) CoordianteButton.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>().text = output.Coordinate.ToString();
        parentButton.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>().text = $"{output.TimeStamp}\n{output.Asset}\n{output.Confidence}\n{output.Coordinate.ToString()}";
    }

    void CoordinateEditPopUpPressed(NetworkOutput output, GameObject parentButton, GameObject CoordianteButton)
    {
        EditColorChange(parentButton);
        if (parentButton.GetComponent<ManipulationHandler>() != null)
        {
            Destroy(parentButton.GetComponent<ManipulationHandler>());
            Destroy(parentButton.GetComponent<NearInteractionGrabbable>());
        }
        else
        {
            parentButton.AddComponent(typeof(NearInteractionGrabbable));
            ManipulationHandler manipulationHander = parentButton.AddComponent(typeof(ManipulationHandler)) as ManipulationHandler;
            manipulationHander.OneHandRotationModeNear = ManipulationHandler.RotateInOneHandType.FaceAwayFromUser;
            manipulationHander.OneHandRotationModeFar = ManipulationHandler.RotateInOneHandType.FaceAwayFromUser;

            manipulationHander.OnManipulationEnded.AddListener((ManipulationEventData test) => { updateCoordinate(output, parentButton, CoordianteButton); });
        }


    }
    public void CreateObjectAtVector(NetworkOutput output)
    {
        GameObject button = Instantiate(TextButton, output.Coordinate, Quaternion.identity);
        button.transform.parent = canvas.transform;
        button.transform.LookAt(MainCamera.transform);
        button.transform.rotation *= Quaternion.Euler(0, 180, 0);
        button.transform.localScale = new Vector3(10000, 10000, 100);
        button.transform.position = output.Coordinate;
        TextMeshPro text = button.transform.Find("IconAndText").Find("TextMeshPro").gameObject.GetComponent<TextMeshPro>();
        text.text = $"{output.TimeStamp}\n{output.Asset}\n{output.Confidence}\n{output.Coordinate.ToString()}";

        button.GetComponent<Interactable>().OnClick.AddListener(() => { ObjectPressed(output, button); });

        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.name = "left";
        left.transform.parent = button.transform;
        left.transform.localPosition = new Vector3(-output.Width / 2, 0, 0) * 0.1f;
        left.transform.localScale = new Vector3(0.01f, output.Height, 0.01f) * 0.1f;
        left.transform.localRotation = Quaternion.identity;

        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.name = "right";
        right.transform.parent = button.transform;
        right.transform.localPosition = new Vector3(output.Width / 2, 0, 0) * 0.1f;
        right.transform.localScale = new Vector3(0.01f, output.Height, 0.01f) * 0.1f;
        right.transform.localRotation = Quaternion.identity;

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "top";
        top.transform.parent = button.transform;
        top.transform.localPosition = new Vector3(0, output.Height / 2, 0) * 0.1f;
        top.transform.localScale = new Vector3(output.Width, 0.01f, 0.01f) * 0.1f;
        top.transform.localRotation = Quaternion.identity;

        GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bot.name = "bot";
        bot.transform.parent = button.transform;
        bot.transform.localPosition = new Vector3(0, -output.Height / 2, 0) * 0.1f;
        bot.transform.localScale = new Vector3(output.Width, 0.01f, 0.01f) * 0.1f;
        bot.transform.localRotation = Quaternion.identity;


        ExistedCoordinates.Add(output.Coordinate);
    }

    public void AddToMessageFlow(string message)
    {
        MessageFlow.Add(message);
        if (MessageFlow.Count > 5)
        {
            MessageFlow.RemoveAt(0);
        }
    }
    void Update()
    {

        foreach (NetworkOutput output in NetworkResults)
        {
            bool existed = false;
            foreach (Vector3 v in ExistedCoordinates)
            {
                if (Vector3.Distance(v, output.Coordinate) < 0.5f)
                {
                    existed = true;
                }
            }
            if (existed) continue;
            // AddToMessageFlow($"{output.TimeStamp}: {output.Asset} found at {output.Coordinate.ToString()} ({output.Confidence}) {output.Width}X{output.Height}");
            AddToMessageFlow($"{output.Coordinate.ToString()} {output.Confidence} {output.Width}X{output.Height}");
            CreateObjectAtVector(output);
        }

        OutputTextMesh.text = string.Join("\n", MessageFlow);

        NetworkResults = new List<NetworkOutput>();
    }

}