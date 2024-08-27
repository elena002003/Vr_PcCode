using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using UnityEngine.InputSystem.Controls;

public class Tablet : NetworkBehaviour
{
    Resolution screenSize;
    [SerializeField]
    GameObject Menue;
    [SerializeField]
    GameObject rayCast;
    [SerializeField]
    GameObject saveButton;
    [SerializeField]
    GameObject clearButton;
    [SerializeField]
    GameObject cursor;
    [SerializeField]
    GameObject colorPicker;
    [SerializeField]
    Material currentColor;
    [SerializeField]
    Material cursorColor;

    [SerializeField]
    Color brushColor = Color.black;
    [SerializeField]
    Color backgroundColor = Color.white;
    [SerializeField]
    int brushSize = 10;
    [SerializeField]
    Vector2Int canvasResulution;
    Texture2D tempCanvasTexture;
    MeshRenderer canvasTexture; // to acess the actual texture of the tablet Canvas
    Vector2 privPoint = Vector2.zero;
    float privPressure = 0;
    Transform tracker; //headset 
    Vector3 headsetPosition;
    bool foundTarget;
    public float posSmoothing = 0.05f;
    public float posMultiplier = 5f;
    float cachedPosMultiplier; //saves posMultiplier when no target is found
    public Vector3 inactiveScreenPos = new Vector3();
    public Vector3 inactiveLookAt = new Vector3();
    public float inactiveScreenSize = 0.1f;
    public float activeScreenSize = 0.5f;
    Vector3 defaultSize = new Vector3(1.77f, 1f, 1f);
    public Texture2D colorPickerImg;
    Vector2 touchStart;
    Vector2 touchEnd;

    void Start()
    {
        //acessTablet.TrackStatusEvent += TargetStatus; //Subscribes the Targetstatus function to TrackStatusEvent
        //tracker = GameObject.Find("ARCamera").transform; //search for arcamera in hierachy and created reference to its transform
        currentColor.color = Color.black;
        cursorColor.color = Color.black;
        //When the tablet spawns attach it to the gameobject "tabletOffset" we created under the camera
        transform.SetParent(GameObject.Find("TabletOffset").transform);
        //reset Position and Rotation 
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(90, -180, 180);
        cachedPosMultiplier = posMultiplier;
        canvasTexture = GetComponent<MeshRenderer>();
        screenSize = Screen.currentResolution;
        // texture saved in memory temporär 
        tempCanvasTexture = new Texture2D(canvasResulution.x, canvasResulution.y, TextureFormat.ARGB32, false);
        canvasTexture.material.mainTexture = tempCanvasTexture;
        Refresh();
    }

    //RUNS ON THE TABLET
    void Update()
    {
        // updates the pen and tracking data every frame  
        if (isLocalPlayer)
        {
            var pen = Pen.current;
            float pressure = pen.pressure.value;
            Vector2 penPosition = pen.position.value;
            CmdRendering(new Vector2(penPosition.x, penPosition.y), pressure, tracker.position);

            //handles gesture input for swiping up and down to show the menu on the tablet
            if (Input.touchCount > 0)
            {
                if (pressure > 0) return;

                //saving the first touch on screen
                Touch touch = Input.GetTouch(0);
                //saving the startingPosition and endPosition of the touch
                if (touch.phase == UnityEngine.TouchPhase.Began)
                {
                    touchStart = touch.position;
                }

                if (touch.phase == UnityEngine.TouchPhase.Ended)
                {
                    touchEnd = touch.position;

                    //calculates direction of the finger stroke
                    float dotProduct = Vector2.Dot((touchEnd - touchStart).normalized, Vector2.down);
                    //downward stroke opens the menue
                    if (dotProduct > 0.8)
                    {
                        OpenMenue(true);
                    } //upward stroke closes the menue
                    else if (dotProduct < -0.8)
                    {
                        OpenMenue(false);
                    }
                }

            }
        }
    }
    //  Gets tablet tracking Status from an Event (to set tabletPos inactive / active)
    [Client]
    public void TargetStatus(bool fndTarget)
    {
        CmdUpdateStatus(fndTarget);
        Debug.Log("Status: " + fndTarget);
    }

    //unsub the Targetstatus function from TrackStatusEvent
    private void OnDisable()
    {
        //references class on the tablet 
        //acessTablet.TrackStatusEvent -= TargetStatus;
    }

    //RUNS ON THE SERVER
    //  Gets tablet tracking Status from an Event (to set tabletPos inactive / active)
    [Command]
    void CmdUpdateStatus(bool status)
    {
        foundTarget = status;
        Debug.Log("res " + status);
    }

    
    //public void Drawing(Vector2 coords, float press, Vector3 hsPosition, NetworkConnectionToClient sender = null)
    //{
    //    Paint(coords, press, hsPosition);
    //}

    //toggles the menu when the correct gesture is detected
    [Command]
    void OpenMenue(bool toggle)
    {
        Menue.SetActive(toggle);
    }

    [Command]
    private void CmdRendering(Vector2 textureCoord, float pressure, Vector3 hsPos)
    {   
        //Ramap laptop resolution to VR canvas resolution
        Vector2 pixelUV = new Vector2(Remap(textureCoord.x, 0, 1920, 0, canvasResulution.x), Remap(textureCoord.y, 0, 1080, 0, canvasResulution.y));
        //remap the cursor position to the world coordinates in vr of the tablet
        Vector3 cursorPos = new Vector3(Remap(textureCoord.x, 0, 1920, 5, -5), 0, Remap(textureCoord.y, 0, 1080, 5, -5));
        cursor.transform.localPosition = cursorPos;
        //remap the pen pressure exponentially to the brushsize for a smoother increase 
        brushSize = (int)RemapCurved(pressure, 0, 1, 5, 20, 4);


        //seperate lines when there is no pressure applied
        if (privPressure <= 0) privPoint = pixelUV;

        //render penstrokes to the canvas
        if (pressure > 0 && !Menue.activeSelf)
        {
            DrawLine(privPoint, pixelUV);
            tempCanvasTexture.Apply();
        }

        privPoint = pixelUV;


        //canvas moves to inactive position when its not tracked
        if (!foundTarget)
        {
            //rotation of canvas is reset
            transform.parent.transform.localRotation = Quaternion.Euler(0,180,0);
            
            posMultiplier = 1;
            hsPos = inactiveScreenPos;
            transform.localScale = defaultSize * inactiveScreenSize;
        }
        //canvas looks at main camera when active
        else
        {
            transform.parent.transform.LookAt(transform.parent.transform.parent, Vector3.up);
            posMultiplier = cachedPosMultiplier;
            transform.localScale = defaultSize * activeScreenSize;
        }
        
        //smoothes the tracking position of the tablet and applies it to canvas
        headsetPosition = Vector3.Lerp(headsetPosition, new Vector3(hsPos.x * (-1), hsPos.z, hsPos.y), Time.deltaTime * posSmoothing);
        transform.parent.transform.localPosition = headsetPosition * posMultiplier;

        if (Menue.activeSelf) { 
            //use the pen positon to interact with the menu (colorselect, buttons)
            Ray ray = new Ray(rayCast.transform.position, Vector3.forward);
            RaycastHit hit;
            Debug.DrawRay(rayCast.transform.position, Vector3.forward * 10, Color.green);
            //comparison to only execute once
            if (privPressure <= 0 && pressure > 0)
            {
                if (Physics.Raycast(ray, out hit, 1f))
                {
                    if (hit.collider.gameObject == saveButton)
                    {
                        Debug.Log("Save");
                        TriggerSave();
                    }
                    if (hit.collider.gameObject == clearButton)
                    {
                        Debug.Log("Clear");
                        Refresh();
                    }
                    if (hit.collider.gameObject == colorPicker)
                    {
                        //get and set the brushcolor and cursorcolor from the selected pixel of the colorpicker image in the menu 
                        Vector2 colorCoords = (hit.textureCoord) * new Vector2(colorPickerImg.width, colorPickerImg.height);
                        brushColor = colorPickerImg.GetPixel((int)colorCoords.x, (int)colorCoords.y);
                        currentColor.color = brushColor;
                        cursorColor.color = brushColor;
                    }
                }
            }
        }
        privPressure = pressure;

        // old code
        //for (int x = 0; x < brushTexture.width; x++)
        //{   
        //    //setzt das bild des kreis zentral in die mitte der spitze vom stift , da ich eig links oben anfange zu malen
        //    //ist die Verschiebungszahl in x richtung also die h�lfte der breite des bildes
        //    float pixelX = (pixelUV.x + x - brushTexture.width / 2);
        //    //schaut ob der pixel aus dem canvas raus schaut dann skippt er den rest der seite und geht zur n�chsten iteration
        //    if (pixelX < 0 || pixelX >= writtenTextTexture.width) 
        //        continue;
        //    for (int y = 0; y < brushTexture.height; y++)
        //    {
        //        float pixelY = (pixelUV.y + y - brushTexture.height / 2);
        //        if (pixelY < 0 || pixelY >= writtenTextTexture.height)
        //            continue;
        //        Color brushPixel = brushTexture.GetPixel(x, y) * brushColor;
        //        // alpha == transperenz , wenn ein pixel im Bereich des Kreises der nicht transperent ist, dann 
        //        if (brushPixel.a > 0)
        //        {
        //            tempTexture.SetPixel((int)pixelX, (int)pixelY, brushPixel);
        //        }
        //    }
        //}

    }

    // connecting 2 cord points with a line
    // Bresenham Line Algorithm
    void DrawLine(Vector2 start, Vector2 end)
    {
        int x0 = (int)start.x;
        int y0 = (int)start.y;
        int x1 = (int)end.x;
        int y1 = (int)end.y;

        int distanceX = Mathf.Abs(x1 - x0);
        int distanceY = Mathf.Abs(y1 - y0);
        //if xstart < xend then draw a line to the right otherwise draw it to the left
        //if ystart < yend then draw a line upwards otherwise draw it downwards
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        //informs when we need to move in the y direction 
        int err = distanceX - distanceY;

        //continue drawing til the endpoint 
        while (true)
        {
            DrawBrush(x0, y0);
            //if currentcorrdinates are the endpointcorrdinates break out of the loop
            if (x0 == x1 && y0 == y1) break;
            //tells us to what direction to move to
            int e2 = err * 2;
            //move horizontal in x direction
            if (e2 > -distanceY)
            {
                err -= distanceY;
                x0 += sx;
            }
            //move vertical in y direction
            if (e2 < distanceX)
            {
                err += distanceX;
                y0 += sy;
            }
        }
    }

    void DrawBrush(int x, int y)
    //int x,y centercoords of the quadtexture
    {
        for (int i = -brushSize; i < brushSize; i++) //x starts top left
        {
            for (int j = -brushSize; j < brushSize; j++) //y
            {
                Vector2 center = new Vector2(x, y);
                Vector2 currentPixel = new Vector2(i + x, j + y);
                //when the value is bigger than the radus of the cycle for the brushsize then dont set the pixel otherwise set the pixel
                if ((currentPixel - center).magnitude < brushSize / 2)
                {
                    // wenn wir im screen bereich sind dann fülle den pixel aus 
                    if (x + i < tempCanvasTexture.width && x + i >= 0 && y + j < tempCanvasTexture.height && y + j >= 0) 
                    {
                        tempCanvasTexture.SetPixel(x + i, y + j, brushColor);
                    }
                }

            }
        }
    }

    //set all Pixels to backgroundcolor
    void Refresh()
    {
        for (int x = 0; x < canvasResulution.x; x++)
        {
            for (int y = 0; y < canvasResulution.y; y++)
            {
                tempCanvasTexture.SetPixel(x, y, backgroundColor);
            }
        }
        canvasTexture.material.mainTexture = tempCanvasTexture;
        tempCanvasTexture.Apply();
    }

    //saves texture to file as PNG or JPG
    void TriggerSave()
    {
        SaveTexture2DToFile(tempCanvasTexture, "C:/Users/EHaib/Downloads/output");
    }

    public enum SaveTextureFileFormat
    {
        JPG, PNG
    };

    /// <summary>
    /// Saves a Texture2D to disk with the specified filename and image format
    /// </summary>
    /// <param name="tex"></param>
    /// <param name="filePath"></param>
    /// <param name="fileFormat"></param>
    /// <param name="jpgQuality"></param>
    static public void SaveTexture2DToFile(Texture2D tex, string filePath, SaveTextureFileFormat fileFormat = SaveTextureFileFormat.PNG, int jpgQuality = 95)
    {
        switch (fileFormat)
        {
            case SaveTextureFileFormat.JPG:
                System.IO.File.WriteAllBytes(filePath + ".jpg", tex.EncodeToJPG(jpgQuality));
                break;
            case SaveTextureFileFormat.PNG:
                System.IO.File.WriteAllBytes(filePath + ".png", tex.EncodeToPNG());
                break;

        }
    }

    public static float Remap(float value, float inputMin, float inputMax, float outputMin, float outputMax)
    {
        // Avoid division by zero
        if (math.abs(inputMax - inputMin) < float.Epsilon)
        {
            return outputMin;
        }

        // Calculate the ratio between the input range and the output range
        float scale = (outputMax - outputMin) / (inputMax - inputMin);

        // Remap the value based on the ratio
        return outputMin + scale * (value - inputMin);
    }

    
    //ajusts linethickness to the pen pressure
    public static float RemapCurved(float value, float inputMin, float inputMax, float outputMin, float outputMax, float exponent)
    {
        // Avoid division by zero
        if (math.abs(inputMax - inputMin) < float.Epsilon)
        {
            return outputMin;
        }

        // Normalize the input value to a 0-1 range
        float t = (value - inputMin) / (inputMax - inputMin);

        // Apply the power function to create a curve
        float curvedT = Mathf.Pow(t, exponent);

        // Scale the curved value to the output range
        return outputMin + curvedT * (outputMax - outputMin);
    }
}