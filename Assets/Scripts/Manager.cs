﻿using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class Manager : MonoBehaviour
{

    public struct Point
    {
        public Vector3 position;
        public Vector3 currentPos;
        public float value;
        public Color color;
        public Color voxColor;
    }

    public Point[,,] points;


    public GameObject vMesh;
    private bool loaded = false;
    public Texture2D colorMap;
    public Color[] gradient;
    private float min = -3f;
    private float max = 3f;

    public GameObject longitude_slider;
    public GameObject latitude_slider;
    public GameObject elevation_slider;
    public GameObject voxelize_button;
    public Slider elevationScale_slider;
    public Toggle snap_toggle;
    public Toggle contour_toggle;
    public Toggle showVolume_toggle;
    public Toggle showShell_toggle;
    public GameObject VoxelMeshScrollView;
    public GameObject VoxelContentPanel;
    public GameObject VoxelViewPrefab;
    private List<GameObject> VoxelViewSet;

    private List<GameObject> voxelSet;
    public List<GameObject> colorList;
    public GameObject cam;

    public Material voxelMat;
    private int lonSet = 0;
    private int latSet = 0;
    private int elevSet = 0;

    private bool settingState = false;
    public GameObject primCube;
    float scale = 10f;
    private Vector2 lonRange;
    private Vector2 latRange;
    private Vector2 topoLonRange;
    private Vector2 topoLatRange;
    private Vector2 topoEleRange;

    private RectTransform voxelLoading;
    private float elevationScale;
    Vector2Int lonSetBounds, latSetBounds, elevSetBounds;

    private bool lonLatView = true;
    public GameObject colorProfilePrefab;
    private bool intGradient;
    private bool bBox;
    public bool contourEnabled;
    private bool shading;
    private bool showingTopography;
    private Mesh[] meshes;
    private Mesh mesh;
    public GameObject[] meshObj;
    public Material meshMat;
    public GameObject topography;
    public Transform ColorMaterialContents;
    private Vector3[,] topographyLatLong;
    private Vector2 topoDimens;

    public GameObject colorPanel;
    public GameObject cPicker;
    public bool colorChangeLock = false;
    public static Manager instance = null;
    // Use this for initialization
    void Start()
    {
        //Set up global
        //Set singleton pattern for Manager
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(gameObject);

        elevationScale = elevationScale_slider.minValue;
        showingTopography = true;
        voxelLoading = voxelize_button.transform.GetChild(0).GetComponent<RectTransform>();

        // Create a default color profile
        gradient = colorMap.GetPixels();

     
        float range = 256f / (max - min);
        float maxmin1 = max - 1f;

        colorList = new List<GameObject>();
        float[] clampValues = new float[] { -3f, -2, -1, 0, 1, 2, 3 };
        for (int i = 0; i < clampValues.Length-1; ++i)
        {
            GameObject cp = Instantiate(colorProfilePrefab, ColorMaterialContents);
            Color col  =  gradient[(int)((Contour(ref min, ref maxmin1, ref clampValues[i]) + (float)max) * range)];
            cp.GetComponent<ChangeMyColor>().Create(clampValues[i], clampValues[i+1], col);
            colorList.Add(cp);
            
        }



        StartCoroutine(ParseData("x.dat"));
        //StartCoroutine(ParseData("Endeavor.dat"));

    }

    public delegate float DelVal(ref float low, ref float high, ref float val);

    private static float Contour(ref float low, ref float high, ref float val)
    {
        float rVal = Mathf.Round(val);
        return (rVal < low) ? low : (rVal > high) ? high : rVal;
    }

    private static float Gradient(ref float low, ref float high, ref float val)
    {
        return (val < low) ? low : (val > high) ? high : val;
    }

    public void ShowTopography()
    {
        showingTopography = !showingTopography;
        topography.SetActive(showingTopography);
    }

    public int GetLongestSide()
    {
        int longest = lonSetBounds.y;
        if (latSetBounds.y > longest)
        {
            longest = latSetBounds.y;
        }
        if (elevSetBounds.y > longest)
        {
            longest = elevSetBounds.y;
        }
        return longest;
    }

    public void ScaleElevation()
    {
        elevationScale = elevationScale_slider.value;
        StartCoroutine(RebuildTopography());
    }

    public void BoundingBox()
    {
        bBox = !bBox;

        meshObj[6].SetActive(bBox);
        meshObj[7].SetActive(bBox);
        meshObj[8].SetActive(bBox);
        meshObj[9].SetActive(bBox);
    }

    public void SnapToBB()
    {
        

        Vector3 newPos;
        
        if (lonLatView)
        {
            newPos = (snap_toggle.isOn) ? new Vector3((-latRange.x - (latRange.y - latRange.x) * 0.5f) * scale  , elevSet * .5f, (-lonRange.x - (lonRange.y - lonRange.x) * 0.5f) * scale) :
                                          new Vector3(-(latRange.x - (latRange.y - latRange.x) * (latSetBounds.y + latSetBounds.x) / latSet * .5f) * scale, (elevSetBounds.y - elevSetBounds.x) * .5f + elevSetBounds.x, -(lonRange.x - (lonRange.y - lonRange.x) * (lonSetBounds.y + lonSetBounds.x) / lonSet * .5f) * scale);

        }
        else
        {
            newPos = (snap_toggle.isOn) ? new Vector3(-(latSet-1) * .5f, (elevSet - 1) * .5f, -(lonSet - 1) * .5f) :
                                          new Vector3(-(latSetBounds.x + (latSetBounds.y - latSetBounds.x) * .5f), (elevSetBounds.y - elevSetBounds.x) * .5f + elevSetBounds.x, -(lonSetBounds.x + (lonSetBounds.y - lonSetBounds.x) * .5f));
           
        }

        this.transform.localPosition = newPos;
        for (int i = 0; i < 6; ++i)
        {
            meshObj[i].transform.localPosition = newPos;
        }

        //topography.transform.localPosition = new Vector3(newPos.x, newPos.y + 10f, newPos.z);

        /*(snap_toggle.isOn) ? new Vector3((-topoLatRange.x - (topoLatRange.y - topoLatRange.x) * 0.5f) * scale, elevSet * .5f, (-topoLonRange.x - (topoLonRange.y - topoLonRange.x) * 0.5f) * scale) :
                                          new Vector3(-(latRange.x - (latRange.y - latRange.x) * (latSetBounds.y + latSetBounds.x) / latSet * .5f) * scale, (elevSetBounds.y - elevSetBounds.x) * .5f + elevSetBounds.x, -(lonRange.x - (lonRange.y - lonRange.x) * (lonSetBounds.y + lonSetBounds.x) / lonSet * .5f) * scale);
                                          */
        //topography.transform.localPosition = new Vector3(-(topoLonRange.x * scale) + newPos.x - Mathf.Abs(topoLonRange.x - latRange.x) * scale, elevSet + (topoEleRange.y - topoEleRange.x) * 0.001f - newPos.y, -(topoLatRange.x * scale) + newPos.z - Mathf.Abs(topoLatRange.x - lonRange.x) * scale);

        /*
        if (voxelSet != null)
        {
            for (int i = 0; i < voxelSet.Count; ++i)
            {
                voxelSet[i].transform.localPosition = newPos;
            }
        }
        */
    }

    public void ShowShell()
    {
        bool show = showShell_toggle.isOn;

        if (meshObj != null)
        {
            for (int i = 0; i < 6; ++i)
            {
                meshObj[i].SetActive(show);
            }
        }
    }

    public void ShowVolume()
    {
        bool show = showVolume_toggle.isOn;

        if (voxelSet != null)
        {
            for (int i = 0; i < voxelSet.Count; ++i)
            {
                voxelSet[i].SetActive(show);
            }
        }
    }


    public IEnumerator Reshade(bool VOX)
    {
        DelVal Shading;
        if (contourEnabled)
        {
            Shading = Contour;
        }
        else
        {
            Shading = Gradient;
        }

        if (shading == true) { shading = true; yield break; };



        Debug.Log("Vox Shading: " + VOX);
        
        int lat = latSet - 1;
        int ele = elevSet - 1;
        int lon = lonSet - 1;


        // Default gradient - fix later to be a gradient on available colors
        // By interpolation between available colors spread evenly through the gradient
        float range = 256f / (max - min);
        float maxmin1 = max - 1f;


        int iS = latSetBounds.x;
        int iE = latSetBounds.y;
        int jS = elevSetBounds.x;
        int jE = elevSetBounds.y;
        int kS = lonSetBounds.x;
        int kE = lonSetBounds.y;

        if (VOX == true)
        {
            //Clear voxelColors
            for (int i = 1; i < lat; ++i)
            {
                for (int j = 1; j < ele; ++j)
                {
                    for (int k = 1; k < lon; ++k)
                    {
                        points[i, j, k].voxColor = Color.clear;
                    }
                }
            }

            Debug.Log("Vox Shading");
            //Correct shading to clamped areas

            for (int i = iS; i < iE; ++i)
            {
                for (int j = jS; j < jE; ++j)
                {
                    for (int k = kS; k < kE; ++k)
                    {
                        float v = points[i, j, k].value;

                        for(int t=0; t< colorList.Count; ++t)
                        {
                            if(colorList[t].GetComponent<ChangeMyColor>().CanUse(v))
                            {
                                points[i, j, k].voxColor = colorList[t].GetComponent<ChangeMyColor>().color;
                                t = colorList.Count; // safe break
                            }
                        }
                    }
                }
            }

        }
        else
        {
            for (int i = 1; i < lat; ++i)
            {
                for (int j = 1; j < ele; ++j)
                {
                    for (int k = 1; k < lon; ++k)
                    {
                        points[i, j, k].color = gradient[(int)((Shading(ref min, ref maxmin1, ref points[i, j, k].value) + (float)max) * range)];
                    }
                }
            }
        }


        shading = false;
        yield return RebuildMesh();

    }

    public void SetContour()
    {
        contourEnabled = !contourEnabled;

        StartCoroutine(Reshade(false));
    }

    public IEnumerator ParseData(string fileName)
    {
        //Debug.Log("Loading Data");

        lonRange = new Vector2(float.MaxValue, float.MinValue);
        latRange = new Vector2(float.MaxValue, float.MinValue);
        topoEleRange = new Vector2(float.MaxValue, float.MinValue);
        int y;
        int x;
        int total = 1;
        
        StreamReader inp_stm = new StreamReader(Application.dataPath + "/Resources/" + fileName);


        string inp_ln;

        /*
        1) longitude min
        2) longitude max
        3) latitude min
        4) latitude max
        5) increment in longitude
        6) increment in latitude
        7) length longitude
        8) length latitude
        */

        // Get topography header data
        inp_ln = inp_stm.ReadLine();
        List<string> topographyHeader = new List<string>(inp_ln.Split(' '));

        Debug.Log(inp_ln);

        topoLonRange = new Vector2(Convert.ToSingle(inp_stm.ReadLine()), Convert.ToSingle(inp_stm.ReadLine()));
        topoLatRange = new Vector2(Convert.ToSingle(inp_stm.ReadLine()), Convert.ToSingle(inp_stm.ReadLine()));
        float m_inc = Convert.ToSingle(inp_stm.ReadLine());
        float n_inc = Convert.ToSingle(inp_stm.ReadLine());
        float lonInc = (lonLatView)? m_inc: 1.0f;
        float latInc = (lonLatView) ? n_inc : 1.0f;
        x = Convert.ToInt32(inp_stm.ReadLine());
        y = Convert.ToInt32(inp_stm.ReadLine());
        topographyLatLong = new Vector3[y, x];
        topoDimens = new Vector2(y, x);

        inp_ln = inp_stm.ReadLine();

        List<string> elevationData = new List<string>(inp_ln.Split(' '));
        if (!(Convert.ToInt32(elevationData[0]) == x) || !(Convert.ToInt32(elevationData[1]) == y)) Debug.Log("error in topoSize");


        Debug.Log("lonInc: " + lonInc + "   latInc: " + latInc);
        for (int j = 0; j < y; ++j)
        {
            float ji = (float)j * lonInc;
            for (int i = 0; i < x; ++i)
            {
                float ii = (float)i * lonInc;
                float val = Convert.ToSingle(inp_stm.ReadLine());
                topographyLatLong[j, i] = new Vector3(ii,val,ji);
                if (val < topoEleRange.x) topoEleRange.x = val; else if (val > topoEleRange.y) topoEleRange.y = val;
            }
        }
        

        // Get velocity Latitude data

        inp_ln = inp_stm.ReadLine();
        List<string> dimens = new List<string>(inp_ln.Split(' '));
        //Debug.Log("Velocity Latitude dimensions: " + inp_ln);

        Vector2[,] latlong;
        if (dimens.Count != 2)
        {
            Debug.LogError("Latitude dimensions are not correct: " + dimens.Count);
            for (int i = 0; i < dimens.Count; ++i)
            {
                Debug.Log("index " + i + ": " + dimens[i]);
            }
            yield break;
        }

        y = Convert.ToInt32(dimens[0]);
        x = Convert.ToInt32(dimens[1]);
        latlong = new Vector2[x, y];

        Debug.Log(x + "..." + y);

        // MatLab is column major order -> z,y,x

        for (int j = 0; j < x; ++j)
        {
            for (int i = 0; i < y; ++i)
            {
                float val = Convert.ToSingle(inp_stm.ReadLine());
                latlong[j, i].y = val;
                if (val < lonRange.x) lonRange.x = val; else if (val > lonRange.y) lonRange.y = val;
            }
        }


        // Get Longitude data

        inp_ln = inp_stm.ReadLine();
        dimens = new List<string>(inp_ln.Split(' '));

        //Debug.Log("Velocity Longitude dimensions: " + inp_ln);
        if (dimens.Count != 2)
        {
            Debug.LogError("Longitude dimensions are not correct: " + dimens.Count);
            for (int i = 0; i < dimens.Count; ++i)
            {
                Debug.Log("index " + i + ": " + dimens[i]);
            }
            yield break;
        }


        y = Convert.ToInt32(dimens[0]);
        x = Convert.ToInt32(dimens[1]);
        // MatLab is column major order -> z,y,x

        for (int j = 0; j < x; ++j)
        {
            for (int i = 0; i < y; ++i)
            {
                float val = Convert.ToSingle(inp_stm.ReadLine());
                latlong[j, i].x = val;
                if (val < latRange.x) latRange.x = val; else if (val > latRange.y) latRange.y = val;
            }
        }

        //Debug.Log("Starting latRange: " + latRange.ToString());
        //Debug.Log("Starting lonRange: " + lonRange.ToString());
        
        // Get Velocity data

        inp_ln = inp_stm.ReadLine();
        dimens = new List<string>(inp_ln.Split(' '));


        if (dimens.Count != 3)
        {
            Debug.LogError("Dimensions are not correct: " + dimens.Count);
            for (int i = 0; i < dimens.Count; ++i)
            {
                Debug.Log("index " + i + ": " + dimens[i]);
            }
            yield break;
        }
        int[] dim = new int[dimens.Count];

        total = 1;

        for (int i = 0; i < dimens.Count; ++i)
        {
            dim[i] = Convert.ToInt32(dimens[i]);
            total *= dim[i];
        }

        //Populate data matrix O(n^3)


        // Add 2 to each dimension for voxelize buffering later
        lonSet = dim[0] + 2;
        latSet = dim[1] + 2;
        elevSet = dim[2] + 2;

        points = new Point[latSet, elevSet, lonSet];
        //Debug.Log("lonSet: " + lonSet + "  latSet: " + latSet + "  elevSet: " + elevSet + " dim[0]: " + dim[0] + " dim[1]: " + dim[1] + " dim[2]: " + dim[2]);

        // MatLab is column major order -> z,y,x

        int ele = elevSet - 1;
        int ele2 = ele - 1;
        int lon = lonSet - 1;
        int lon2 = lon - 1;
        int lat = latSet - 1;
        int lat2 = lat - 1;
       

        for (int i = 0; i < elevSet; ++i)
        {
            float e = -i * 0.5f;
            for (int j = 0; j < latSet; ++j)
            {
                int jm1 = j - 1;
                for (int k = 0; k < lonSet; ++k)
                {
                    int km1 = k - 1;
                    Point tp = new Point();
                    if (i > 0 && i < ele && j > 0 && j < lat && k > 0 && k < lon) // make space for the shell
                    {
                        //tp.position = new Vector3(latlong[jm1, km1].x, elev[jm1, km1], latlong[jm1, km1].y);
                        tp.position = new Vector3(latlong[jm1, km1].x, e, latlong[jm1, km1].y);
                        tp.value = Convert.ToSingle(inp_stm.ReadLine());
                        points[j, i, k] = tp;
                    }
                }
            }
        }

        inp_stm.Close();

        lonSetBounds = new Vector2Int(1, lonSet - 2);
        latSetBounds = new Vector2Int(1, latSet - 2);
        elevSetBounds = new Vector2Int(1, elevSet - 2);


        // Lerp outer shell to finish cube
        // Doing it here saves a lot of processing time later when voxelizing
        
        //Top
        for (int i = 1; i < lat; ++i)
        {
            for (int j = 1; j < lon; ++j)
            {
                Vector3 vr = points[i, 1, j].position; // one point below
                Point tp = new Point
                {
                    position = new Vector3(vr.x, vr.y, vr.z),
                    value = float.NaN
                };
                points[i, 0, j] = tp;
            }
        }

        //Bottom
        for (int i = 1; i < lat; ++i)
        {
            for (int j = 1; j < lon; ++j)
            {
                Vector3 vr = points[i, ele2, j].position; // one point above
                Point tp = new Point
                {
                    position = new Vector3(vr.x, vr.y, vr.z),
                    value = float.NaN
                };
                points[i, ele, j] = tp;
            }
        }

        //Left
        for (int i = 1; i < lat; ++i)
        {
            for (int j = 1; j < ele; ++j)
            {
                Vector3 vr = points[i, j, 1].position; // one point to the right
                Point tp = new Point
                {
                    position = new Vector3(vr.x, vr.y, vr.z),
                    value = float.NaN
                };
                points[i, j, 0] = tp;
            }
        }

        //Right
        for (int i = 1; i < lat; ++i)
        {
            for (int j = 1; j < ele; ++j)
            {
                Vector3 vr = points[i, j, lon2].position; // one point to the left
                Point tp = new Point
                {
                    position = new Vector3(vr.x, vr.y, vr.z),
                    value = float.NaN
                };
                points[i, j, lon] = tp;
            }
        }

        //Front
        for (int i = 1; i < ele; ++i)
        {
            for (int j = 1; j < lon; ++j)
            {
                Vector3 vr = points[1, i, j].position; // one point to the back
                Point tp = new Point
                {
                    position = new Vector3(vr.x, vr.y, vr.z),
                    value = float.NaN
                };
                points[0, i, j] = tp;
            }
        }

        //Back
        for (int i = 1; i < ele; ++i)
        {
            for (int j = 1; j < lon; ++j)
            {
                Vector3 vr = points[lat2, i, j].position; // one point to the front
                Point tp = new Point
                {
                    position = new Vector3(vr.x, vr.y, vr.z),
                    value = float.NaN
                };
                points[lat, i, j] = tp;
            }
        }
        

        yield return Reshade(false);

        //Assign the slider information

        longitude_slider.GetComponent<SliderConnection>().min.maxValue = lonSet - 2;
        longitude_slider.GetComponent<SliderConnection>().min.minValue = 1;
        longitude_slider.GetComponent<SliderConnection>().max.maxValue = lonSet - 2;
        longitude_slider.GetComponent<SliderConnection>().max.minValue = 1;
        longitude_slider.GetComponent<SliderConnection>().max.value = lonSet - 2;
        longitude_slider.GetComponent<SliderConnection>().min.value = 1;

        latitude_slider.GetComponent<SliderConnection>().min.maxValue = latSet - 2;
        latitude_slider.GetComponent<SliderConnection>().min.minValue = 1;
        latitude_slider.GetComponent<SliderConnection>().max.maxValue = latSet - 2;
        latitude_slider.GetComponent<SliderConnection>().max.minValue = 1;
        latitude_slider.GetComponent<SliderConnection>().max.value = latSet - 2;
        latitude_slider.GetComponent<SliderConnection>().min.value = 1;

        elevation_slider.GetComponent<SliderConnection>().min.maxValue = elevSet - 2;
        elevation_slider.GetComponent<SliderConnection>().min.minValue = 1;
        elevation_slider.GetComponent<SliderConnection>().max.maxValue = elevSet - 2;
        elevation_slider.GetComponent<SliderConnection>().max.minValue = 1;
        elevation_slider.GetComponent<SliderConnection>().max.value = elevSet - 2;
        elevation_slider.GetComponent<SliderConnection>().min.value = 1;


        // Create bounding box

        
        #region BoundingBox

        float xh = latSet * 0.5f;
        float mxh = -xh; --xh;
        float yh = elevSet * 0.5f;
        float myh = (-yh) + 1;
        float zh = lonSet * 0.5f;
        float mzh = -zh; --zh;

        
        LineRenderer lr = meshObj[6].GetComponent<LineRenderer>();
        lr.positionCount = 10;
        lr.SetPositions(
        new Vector3[] {
                    new Vector3(xh, yh, mzh), new Vector3(xh, yh, zh), new Vector3(mxh, yh, zh), new Vector3(mxh, yh, mzh), new Vector3(xh, yh, mzh),
                    new Vector3(xh, myh, mzh), new Vector3(xh, myh, zh), new Vector3(mxh, myh, zh), new Vector3(mxh, myh, mzh), new Vector3(xh, myh, mzh)
        });

        lr = meshObj[7].GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { new Vector3(mxh, myh, mzh), new Vector3(mxh, yh, mzh) });

        lr = meshObj[8].GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { new Vector3(mxh, myh, zh), new Vector3(mxh, yh, zh) });

        lr = meshObj[9].GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { new Vector3(xh, myh, zh), new Vector3(xh, yh, zh) });

        #endregion BoundingBox
        

        SnapToBB();
        // End Create bounding box


        //StartCoroutine(RebuildTopography());
        //Debug.Log("Data Loaded");

        cam.GetComponent<DragMouseOrbit>().SetDistance(GetLongestSide() * 2);
        StartCoroutine(RebuildTopography());

    }
    
    public void AddColors()
    {
        // Add button to list
        GameObject cp = Instantiate(colorProfilePrefab, ColorMaterialContents);
        cp.GetComponent<ChangeMyColor>().color = Color.white;
        colorList.Add(cp);
    }

    public void RemoveColors()
    {
        if(colorList.Count > 0)
        {
            GameObject cp = colorList[colorList.Count - 1];
            Destroy(cp);
            colorList.RemoveAt(colorList.Count - 1);
        }
    }

    // This builds the topography layer
    IEnumerator RebuildTopography()
    {

        int wMin = 0;
        int wMax = (int)topoDimens.y-1;
        int dMin = 0;
        int dMax = (int)topoDimens.x-1;
        
        //Debug.Log("topoLatRange: " + topoLatRange.ToString());
        //Debug.Log("DIF topoLatRange: " + (topoLatRange.y - topoLatRange.x));
        //Debug.Log("topoLonRange: " + topoLonRange.ToString());
        //Debug.Log("DIF topoLonRange: " + (topoLonRange.y - topoLonRange.x));
        //Debug.Log("latRange: " + latRange.ToString());
        //Debug.Log("DIF latRange: " + (latRange.y - latRange.x));
        //Debug.Log("lonRange: " + lonRange.ToString());
        //Debug.Log("DIF lonRange: " + (lonRange.y - lonRange.x));

        float wRatio = (latRange.y - latRange.x) / (topoLatRange.y - topoLatRange.x);
        float dRatio = (lonRange.y - lonRange.x) / (topoLonRange.y - topoLonRange.x);


        //Debug.Log("wRatio: " + wRatio + "dRatio: " + dRatio);


        int wSize = wMax - wMin;
        int dSize = dMax - dMin;
        int x = wSize; int y = dSize;

        int size = (x + 1) * (y + 1);
        
        Vector3[] vertices = new Vector3[size];
        Color[] colors = new Color[size];

        int i, w;
        float elevationScale = elevationScale_slider.value;
        for (i = 0, w = wMin; w <= wMax; w++)
        {
            for (int d = dMin; d <= dMax; d++, i++)
            {
                Vector3 p = topographyLatLong[d,w];
                colors[i] = Color.white;
                //vertices[i] = new Vector3(w, -b + p.y, d);
                vertices[i] = new Vector3(p.x * scale, p.y * elevationScale, p.z * scale);
            }
        }

       // Debug.Log("topoEleRange: " + topoEleRange.ToString());
        float elevationOffset = ((topoEleRange.y - topoEleRange.x) * 0.5f) * elevationScale + meshObj[0].transform.localPosition.y;

        // Fix this to be more accurrate. the +5f is a temp val - not correct
        topography.transform.localPosition = new Vector3(topography.transform.localPosition.x,
            elevationOffset + 5f,
            topography.transform.localPosition.z);
        //Debug.Log("size: " + size + " i:" + i);
        Mesh tMesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            vertices = vertices,
            colors = colors
        };


        int[] triangles = new int[size * 6];
        int ti, vi;
        for (ti = 0, vi = 0, i = 0; i < x; i++, vi++)
        {
            for (int j = 0; j < y; j++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + y + 1;
                triangles[ti + 5] = vi + y + 2;
            }
        }
        tMesh.triangles = triangles;
        tMesh.RecalculateNormals();
        tMesh.RecalculateTangents();
        topography.GetComponent<MeshFilter>().mesh = tMesh;

        yield return null;

    }

    // This builds the Square Mesh - Input bounds for x, y, z axis
    IEnumerator RebuildMesh()
    {
        meshes = new Mesh[6];

        for (int i = 0; i < 6; ++i)
        {
            meshObj[i].GetComponent<MeshFilter>().mesh = GetFaceMesh(i);
        }

        yield return null;

    }

    private Mesh GetFaceMesh(int face)
    {

        //float a, b, c, d, e, f = 0f;
        int x = 0;
        int y = 0;

        int wSize = latSetBounds.y - latSetBounds.x;
        int hSize = elevSetBounds.y - elevSetBounds.x;
        int dSize = lonSetBounds.y - lonSetBounds.x;

        
        switch (face)
        {
            case 0: x = wSize; y = dSize; break;  // Top 
            case 1: x = wSize; y = dSize; break;  // Bottom
            case 2: x = wSize; y = hSize; break;  // Left 
            case 3: x = wSize; y = hSize; break;  // Right
            case 4: x = hSize; y = dSize; break;  // Front
            case 5: x = hSize; y = dSize; break;  // Back
        }

        int size = (x+1) * (y+1);
        Vector3[] vertices = new Vector3[size];
        Color[] colors = new Color[size];

        //Debug.Log("latSetBounds: " + latSetBounds.ToString());
        //Debug.Log("lonSetBounds: " + lonSetBounds.ToString());
        if (face == 0) // Top
        {
            int b = elevSetBounds.x;
            for (int i = 0, w = latSetBounds.x; w <= latSetBounds.y; w++)
            {
                for (int d = lonSetBounds.x; d <= lonSetBounds.y; d++, i++)
                {
                    Vector3 p = points[w, b, d].position;
                    //if (counter < 1000 &&  d == lonSetBounds.y) { counter++; Debug.Log(i + ":  " + p.ToString("F6")); }
                    colors[i] = points[w, b, d].color;
                    vertices[i] = (lonLatView)? new Vector3(p.x * scale, -b + p.y, p.z * scale): 
                        new Vector3(w * scale, -b + p.y * elevationScale, d * scale);
                }
            }
        }
        else if (face == 1) // Bottom
        {
            int b = elevSetBounds.y;
            for (int i = 0, w = latSetBounds.x; w <= latSetBounds.y; w++)
            {
                for (int d = lonSetBounds.x; d <= lonSetBounds.y; d++, i++)
                {
                    Vector3 p = points[w, b, d].position;
                    colors[i] = points[w, b, d].color;
                    vertices[i] = new Vector3(p.x * scale, -b + p.y, p.z * scale);
                    //vertices[i] = new Vector3(p.x, -b + p.y, p.z);
                }
            }
        }
        else if (face == 2) // Left
        {
            int b = lonSetBounds.x;
            for (int i = 0, w = latSetBounds.x; w <= latSetBounds.y; w++)
            {
                for (int h = elevSetBounds.x; h <= elevSetBounds.y; h++, i++)
                {
                    Vector3 p = points[w, h, b].position;
                    colors[i] = points[w, h, b].color;
                    vertices[i] = new Vector3(p.x * scale, -h + p.y, p.z * scale);
                    //vertices[i] = new Vector3(w, -h + p.y, b);

                }
            }
        }  
        else if (face == 3) // Right
        {
            int b = lonSetBounds.y;
            for (int i = 0, w = latSetBounds.x; w <= latSetBounds.y; w++)
            {
                for (int h = elevSetBounds.x; h <= elevSetBounds.y; h++, i++)
                {
                    Vector3 p = points[w, h, b].position;
                    colors[i] = points[w, h, b].color;
                    vertices[i] = new Vector3(p.x * scale, -h + p.y , p.z * scale);
                    //vertices[i] = new Vector3(w, -h + p.y, b);
                }
            }
        } 
        else if (face == 4) // Front
        {
            int b = latSetBounds.x;
            for (int i = 0, h = elevSetBounds.x; h <= elevSetBounds.y; h++)
            {
                for (int d = lonSetBounds.x; d <= lonSetBounds.y; d++, i++)
                {
                    Vector3 p = points[b, h, d].position;
                    colors[i] = points[b, h, d].color;
                    vertices[i] = new Vector3(p.x * scale, -h + p.y, p.z * scale);
                    //vertices[i] = new Vector3(b, -h + p.y, d);
                }
            }
        }
        else if (face == 5) // Back
        {
            int b = latSetBounds.y;
            for (int i = 0, h = elevSetBounds.x; h <= elevSetBounds.y; h++)
            {
                for (int d = lonSetBounds.x; d <= lonSetBounds.y; d++, i++)
                {
                    Vector3 p = points[b, h, d].position;
                    colors[i] = points[b, h, d].color;
                    vertices[i] = new Vector3(p.x * scale, -h + p.y, p.z * scale);
                    //vertices[i] = new Vector3(b, -h + p.y, d);
                }
            }
        }

        mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            vertices = vertices,
            colors = colors
        };

        int[] triangles = new int[size * 6];
        for (int ti = 0, vi = 0, i = 0; i < x; i++, vi++)
        {
            for (int j = 0; j < y; j++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + y + 1;
                triangles[ti + 5] = vi + y + 2;
            }
        }
        mesh.triangles = triangles;
        return mesh;
    }

    public void ChangeSlider(int i)
    {

        switch (i)
        {
            //Longitude
            case 0:
                lonSetBounds = new Vector2Int((int)longitude_slider.GetComponent<SliderConnection>().min.value, (int)longitude_slider.GetComponent<SliderConnection>().max.value);
                break;

            // Latitude
            case 1:
                latSetBounds = new Vector2Int((int)latitude_slider.GetComponent<SliderConnection>().min.value, (int)latitude_slider.GetComponent<SliderConnection>().max.value);
                break;

            // Elevation
            case 2:
                elevSetBounds = new Vector2Int((int)elevation_slider.GetComponent<SliderConnection>().min.value, (int)elevation_slider.GetComponent<SliderConnection>().max.value);
                break;
        }

        SnapToBB();

        StartCoroutine(RebuildMesh());

    }

    public IEnumerator SetVoxelState()
    {
        while (shading == true) yield return null;
        //Arbitrarily start with face 0 , top down build

        //Debug.Log("SetVoxelState elevSet: " + elevSet + " lonSet: " + lonSet + " latSet: " + latSet);
        
        for (int x = 0; x < latSet; x++)
        {
            for (int y = 0; y < elevSet; y++)
            {
                for (int z = 0; z < lonSet; z++)
                {
                    Vector3 t = points[x, y, z].position;
                    //points[x, y, z].currentPos = new Vector3(t.x*scale, (float)(-y + t.y) , t.z * scale);
                    points[x, y, z].currentPos = new Vector3(t.x * scale, (float)(-y + t.y), t.z * scale);
                }
            }
        }
        
        yield return null;
        
    }

    struct GRIDCELL
    {
        public Vector3[] p;
        public float[] val;
    }

    public void VoxelizeButton()
    {
        StartCoroutine(VoxPrep());
    }

    public IEnumerator VoxPrep()
    {

        if (voxelSet == null)
        {
            voxelSet = new List<GameObject>();
        }
        else
        {
            for (int i = 0; i < voxelSet.Count; ++i)
            {
                Destroy(voxelSet[i]);
            }
            voxelSet.Clear();
        }

        // Destroy objects in list to refresh the view with new updated list

        if (VoxelViewSet == null)
        {
            VoxelViewSet = new List<GameObject>();
        }
        else
        {
            int childCount = VoxelViewSet.Count;
            for (int i = 0; i < childCount; ++i)
            {
                Destroy(VoxelViewSet[i]);
            }
            VoxelViewSet.Clear();
        }

        VoxelMeshScrollView.SetActive(false);

        yield return Reshade(true);
        //while (shading == true) yield return null;

        // Set the current state to be voxelized
        yield return SetVoxelState();

        //StartCoroutine(Voxelize(Color.clear));
        for (int i = 0; i < colorList.Count; ++i)
        {
            StartCoroutine(Voxelize(colorList[i].GetComponent<ChangeMyColor>().color));
        }

        while (voxelSet.Count < colorList.Count)
        {
            yield return new WaitForSeconds(1f);
        }

        voxelLoading.sizeDelta = Vector2.zero;


        // Build VoxelMenu Set
        VoxelMeshScrollView.SetActive(true);


        // Make a new list
        VoxelContentPanel.GetComponent<RectTransform>().sizeDelta = new Vector2( VoxelContentPanel.GetComponent<RectTransform>().sizeDelta.x, voxelSet.Count * (VoxelViewPrefab.GetComponent<RectTransform>().sizeDelta.y + 2f));
        for (int i=0; i< colorList.Count; ++i)
        {
            GameObject go = Instantiate(VoxelViewPrefab, VoxelContentPanel.transform);
            go.GetComponent<VoxelToggle>().SetData(voxelSet[i], colorList[i].GetComponent<ChangeMyColor>().color, colorList[i].GetComponent<ChangeMyColor>().low.ToString() + "  -  " + colorList[i].GetComponent<ChangeMyColor>().high.ToString());
            VoxelViewSet.Add(go);
        }
    }

    public IEnumerator Voxelize(Color colorval)
    {
        //Debug.Log("Color voxelizing: " + colorval);
        List<Vector3> vertices = new List<Vector3>();
        // Polygonise the grid 

        
        Vector3 t = Vector3.zero;
        int wm1 = latSet - 1;
        int hm1 = elevSet - 1;
        int dm1 = lonSet - 1;

        int iS = latSetBounds.x - 1;
        int iE = latSetBounds.y + 1;
        int jS = elevSetBounds.x - 1;
        int jE = elevSetBounds.y + 1;
        int kS = lonSetBounds.x - 1;
        int kE = lonSetBounds.y + 1;

        int stopStep = 5;
        int[] waitSet = new int[stopStep];
        for(int i = 0; i < stopStep; ++i)
        {
            waitSet[i] = hm1 / (i+1);
        }

        float loadingWidth = voxelize_button.GetComponent<RectTransform>().rect.width;
        float loadingHeight = voxelize_button.GetComponent<RectTransform>().rect.height;
        float div = 1f / ((float)colorList.Count * (float)(iE-iS) * (float)(jE-jS))  * (float)loadingWidth;
        



        // Bind the values in the user moves the sliders while voxelizing

       


        for (int i = iS; i < iE; i++)
        {
            int i1 = i + 1;

            for (int j = jS; j < jE; j++)
            {
                int j1 = j + 1;

                for (int k = kS; k < kE; k++)
                {
                    int k1 = k + 1;
                    GRIDCELL grid = new GRIDCELL
                    {
                        p = new Vector3[8],
                        val = new float[8]
                    };

                    grid.p[0] = points[i, j, k].currentPos;
                    grid.val[0] = points[i, j, k].voxColor == colorval ? 0 : 1;

                    grid.p[1] = points[i1, j, k].currentPos;
                    grid.val[1] = points[i1, j, k].voxColor == colorval ? 0 : 1;

                    grid.p[2] = points[i1, j1, k].currentPos;
                    grid.val[2] = points[i1, j1, k].voxColor == colorval ? 0 : 1;

                    grid.p[3] = points[i, j1, k].currentPos;
                    grid.val[3] = points[i, j1, k].voxColor == colorval ? 0 : 1;

                    grid.p[4] = points[i, j, k1].currentPos;
                    grid.val[4] = points[i, j, k1].voxColor == colorval ? 0 : 1;

                    grid.p[5] = points[i1, j, k1].currentPos;
                    grid.val[5] = points[i1, j, k1].voxColor == colorval ? 0 : 1;

                    grid.p[6] = points[i1, j1, k1].currentPos;
                    grid.val[6] = points[i1, j1, k1].voxColor == colorval ? 0 : 1;

                    grid.p[7] = points[i, j1, k1].currentPos;
                    grid.val[7] = points[i, j1, k1].voxColor == colorval ? 0 : 1;
                    
                    PolygoniseCube(ref grid, 0.5f, ref vertices);
                    
                }
                for (int s = 0; s < stopStep; ++s)
                {
                    if (j == waitSet[s]) yield return null;
                }
                voxelLoading.sizeDelta = new Vector2(voxelLoading.sizeDelta.x + div, loadingHeight);
            }
            
        }

        if (vertices.Count > 0)
        {
            Mesh tm = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            // reverse vertices 
            vertices.Reverse();
            tm.vertices = vertices.ToArray();
            tm.triangles = vertices.Select((v, index) => index).ToArray();
            tm.RecalculateNormals();

            Material m = new Material(voxelMat);
            GameObject go = new GameObject(colorval.ToString());
            go.transform.localPosition = this.transform.localPosition;
            go.AddComponent<MeshRenderer>();
            go.GetComponent<MeshRenderer>().allowOcclusionWhenDynamic = false;
            go.AddComponent<MeshFilter>();
            go.GetComponent<MeshFilter>().mesh = tm;
            Color c = new Color(colorval.r, colorval.g, colorval.b, 0.5f);
            m.color = c;
            go.GetComponent<MeshRenderer>().material = m;
            voxelSet.Add(go);
        }

        yield return null;
    }

    private void PolygoniseCube(ref GRIDCELL g, float iso, ref List<Vector3> vertices)
    {
        int i;
        int cubeindex = 0;
        Vector3[] vertlist = new Vector3[12];

        /*
           Determine the index into the edge table which
           tells us which vertices are inside of the surface
        */

        if (g.val[0] < iso) cubeindex |= 1;
        if (g.val[1] < iso) cubeindex |= 2;
        if (g.val[2] < iso) cubeindex |= 4;
        if (g.val[3] < iso) cubeindex |= 8;
        if (g.val[4] < iso) cubeindex |= 16;
        if (g.val[5] < iso) cubeindex |= 32;
        if (g.val[6] < iso) cubeindex |= 64;
        if (g.val[7] < iso) cubeindex |= 128;


        /* Cube is entirely in/out of the surface */
        if (edgeMaskTable[cubeindex] == -1)
            return;
        

        /* Find the vertices where the surface intersects the cube */
        if ((edgeMaskTable[cubeindex] & 1) !=0)
        {
            vertlist[0] = VertexInterp(iso, ref g.p[0], ref g.p[1], ref g.val[0], ref g.val[1]);
        }
        if ((edgeMaskTable[cubeindex] & 2) != 0)
        {
            vertlist[1] = VertexInterp(iso, ref g.p[1], ref g.p[2], ref g.val[1], ref g.val[2]);
        }
        if ((edgeMaskTable[cubeindex] & 4) != 0)
        {
            vertlist[2] = VertexInterp(iso, ref g.p[2], ref g.p[3], ref g.val[2], ref g.val[3]);
        }
        if ((edgeMaskTable[cubeindex] & 8) != 0)
        {
            vertlist[3] = VertexInterp(iso, ref g.p[3], ref g.p[0], ref g.val[3], ref g.val[0]);
        }
        if ((edgeMaskTable[cubeindex] & 16) != 0)
        {
            vertlist[4] = VertexInterp(iso, ref g.p[4], ref g.p[5], ref g.val[4], ref g.val[5]);
        }
        if ((edgeMaskTable[cubeindex] & 32) != 0)
        {
            vertlist[5] = VertexInterp(iso, ref g.p[5], ref g.p[6], ref g.val[5], ref g.val[6]);
        }
        if ((edgeMaskTable[cubeindex] & 64) != 0)
        {
            vertlist[6] = VertexInterp(iso, ref g.p[6], ref g.p[7], ref g.val[6], ref g.val[7]);
        }
        if ((edgeMaskTable[cubeindex] & 128) != 0)
        {
            vertlist[7] = VertexInterp(iso, ref g.p[7], ref g.p[4], ref g.val[7], ref g.val[4]);
        }
        if ((edgeMaskTable[cubeindex] & 256) != 0)
        {
            vertlist[8] = VertexInterp(iso, ref g.p[0], ref g.p[4], ref g.val[0], ref g.val[4]);
        }
        if ((edgeMaskTable[cubeindex] & 512) != 0)
        {
            vertlist[9] = VertexInterp(iso, ref g.p[1], ref g.p[5], ref g.val[1], ref g.val[5]);
        }
        if ((edgeMaskTable[cubeindex] & 1024) != 0)
        {
            vertlist[10] = VertexInterp(iso, ref g.p[2], ref g.p[6], ref g.val[2], ref g.val[6]);
        }
        if ((edgeMaskTable[cubeindex] & 2048) != 0)
        {
            vertlist[11] = VertexInterp(iso, ref g.p[3], ref g.p[7], ref g.val[3], ref g.val[7]);
        }

        /* Create the triangles */


        for (i = 0; i < 16; i++)
        {
            int v = triTable[cubeindex, i];
            if (v == -1)
            {
                break;
            }
            else{
                vertices.Add(vertlist[v]);
            } 
        }

    }

    Vector3 VertexInterp(float isolevel, ref Vector3 p1, ref Vector3 p2, ref float valp1, ref float valp2)
    {
        float mu;
        Vector3 p;

        if (Mathf.Abs(isolevel - valp1) < 0.001f)
        {
            return (p1);
        }
            
        if (Mathf.Abs(isolevel - valp2) < 0.001f)
        {
            return (p2);
        }
            
        if (Mathf.Abs(valp1 - valp2) < 0.001f)
        {
            return (p1);
        }
            
        mu = (isolevel - valp1) / (valp2 - valp1);
        p = new Vector3(p1.x + mu * (p2.x - p1.x), 
                        p1.y + mu * (p2.y - p1.y), 
                        p1.z + mu * (p2.z - p1.z));
        
        return (p);
    }
  
    public static int[,] triTable = new int[,]
    {
/*000*/{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*001*/{0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*002*/{0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*003*/{1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*004*/{1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*005*/{0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*006*/{9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*007*/{2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
/*008*/{3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*009*/{0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*010*/{1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*011*/{1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
/*012*/{3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*013*/{0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
/*014*/{3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
/*015*/{9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*016*/{4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*017*/{4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*018*/{0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*019*/{4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
/*020*/{1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*021*/{3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
/*022*/{9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
/*023*/{2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
/*024*/{8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*025*/{11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
/*026*/{9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
/*027*/{4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
/*028*/{3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
/*029*/{1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
/*030*/{4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
/*031*/{4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
/*032*/{9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*033*/{9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*034*/{0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*035*/{8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
/*036*/{1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*037*/{3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
/*038*/{5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
/*039*/{2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
/*040*/{9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*041*/{0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
/*042*/{0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
/*043*/{2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
/*044*/{10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
/*045*/{4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
/*046*/{5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
/*047*/{5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
/*048*/{9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*049*/{9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
/*050*/{0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
/*051*/{1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*052*/{9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
/*053*/{10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
/*054*/{8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
/*055*/{2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
/*056*/{7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
/*057*/{9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
/*058*/{2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
/*059*/{11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
/*060*/{9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
/*061*/{5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
/*062*/{11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
/*063*/{11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*064*/{10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*065*/{0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*066*/{9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*067*/{1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
/*068*/{1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*069*/{1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
/*070*/{9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
/*071*/{5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
/*072*/{2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*073*/{11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
/*074*/{0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
/*075*/{5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
/*076*/{6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
/*077*/{0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
/*078*/{3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
/*079*/{6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
/*080*/{5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*081*/{4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
/*082*/{1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
/*083*/{10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
/*084*/{6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
/*085*/{1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
/*086*/{8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
/*087*/{7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
/*088*/{3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
/*089*/{5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
/*090*/{0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
/*091*/{9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
/*092*/{8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
/*093*/{5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
/*094*/{0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
/*095*/{6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
/*096*/{10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*097*/{4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
/*098*/{10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
/*099*/{8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
/*100*/{1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
/*101*/{3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
/*102*/{0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*103*/{8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
/*104*/{10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
/*105*/{0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
/*106*/{3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
/*107*/{6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
/*108*/{9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
/*109*/{8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
/*110*/{3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
/*111*/{6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*112*/{7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
/*113*/{0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
/*114*/{10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
/*115*/{10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
/*116*/{1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
/*117*/{2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
/*118*/{7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
/*119*/{7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*120*/{2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
/*121*/{2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
/*122*/{1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
/*123*/{11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
/*124*/{8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
/*125*/{0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*126*/{7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
/*127*/{7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*128*/{7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*129*/{3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*130*/{0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*131*/{8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
/*132*/{10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*133*/{1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
/*134*/{2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
/*135*/{6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
/*136*/{7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*137*/{7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
/*138*/{2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
/*139*/{1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
/*140*/{10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
/*141*/{10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
/*142*/{0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
/*143*/{7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
/*144*/{6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*145*/{3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
/*146*/{8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
/*147*/{9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
/*148*/{6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
/*149*/{1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
/*150*/{4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
/*151*/{10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
/*152*/{8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
/*153*/{0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*154*/{1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
/*155*/{1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
/*156*/{8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
/*157*/{10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
/*158*/{4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
/*159*/{10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*160*/{4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*161*/{0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
/*162*/{5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
/*163*/{11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
/*164*/{9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
/*165*/{6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
/*166*/{7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
/*167*/{3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
/*168*/{7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
/*169*/{9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
/*170*/{3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
/*171*/{6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
/*172*/{9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
/*173*/{1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
/*174*/{4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
/*175*/{7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
/*176*/{6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
/*177*/{3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
/*178*/{0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
/*179*/{6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
/*180*/{1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
/*181*/{0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
/*182*/{11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
/*183*/{6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
/*184*/{5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
/*185*/{9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
/*186*/{1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
/*187*/{1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*188*/{1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
/*189*/{10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
/*190*/{0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*191*/{10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*192*/{11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*193*/{11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
/*194*/{5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
/*195*/{10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
/*196*/{11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
/*197*/{0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
/*198*/{9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
/*199*/{7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
/*200*/{2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
/*201*/{8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
/*202*/{9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
/*203*/{9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
/*204*/{1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*205*/{0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
/*206*/{9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
/*207*/{9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*208*/{5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
/*209*/{5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
/*210*/{0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
/*211*/{10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
/*212*/{2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
/*213*/{0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
/*214*/{0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
/*215*/{9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*216*/{2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
/*217*/{5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
/*218*/{3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
/*219*/{5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
/*220*/{8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
/*221*/{0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*222*/{8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
/*223*/{9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*224*/{4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
/*225*/{0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
/*226*/{1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
/*227*/{3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
/*228*/{4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
/*229*/{9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
/*230*/{11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
/*231*/{11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
/*232*/{2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
/*233*/{9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
/*234*/{3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
/*235*/{1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*236*/{4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
/*237*/{4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
/*238*/{4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*239*/{4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*240*/{9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*241*/{3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
/*242*/{0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
/*243*/{3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*244*/{1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
/*245*/{3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
/*246*/{0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*247*/{3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*248*/{2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
/*249*/{9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*250*/{2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
/*251*/{1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*252*/{1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*253*/{0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*254*/{0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
/*255*/{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
    };

    public static int[] edgeMaskTable = new int[]
    {
        0x0  , 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
        0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
        0x190, 0x99 , 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c,
        0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
        0x230, 0x339, 0x33 , 0x13a, 0x636, 0x73f, 0x435, 0x53c,
        0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
        0x3a0, 0x2a9, 0x1a3, 0xaa , 0x7a6, 0x6af, 0x5a5, 0x4ac,
        0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
        0x460, 0x569, 0x663, 0x76a, 0x66 , 0x16f, 0x265, 0x36c,
        0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
        0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0xff , 0x3f5, 0x2fc,
        0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
        0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x55 , 0x15c,
        0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
        0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0xcc ,
        0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
        0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc,
        0xcc , 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
        0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c,
        0x15c, 0x55 , 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
        0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc,
        0x2fc, 0x3f5, 0xff , 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
        0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c,
        0x36c, 0x265, 0x16f, 0x66 , 0x76a, 0x663, 0x569, 0x460,
        0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac,
        0x4ac, 0x5a5, 0x6af, 0x7a6, 0xaa , 0x1a3, 0x2a9, 0x3a0,
        0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c,
        0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x33 , 0x339, 0x230,
        0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c,
        0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x99 , 0x190,
        0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c,
        0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x0
    };

}