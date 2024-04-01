using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oculus.Interaction;

public class InteractiveMap : MonoBehaviour
{
    private GameObject playerModel;
    public GameObject miniPlayer;
    public Transform playerEyes;

    private List<(GameObject Original, GameObject Mini)> movables; // list of tuples representing a reference to the original scene object in first and to the created miniature in second
    private List<(GameObject Original, GameObject Mini)> coins; // list of tuples representing a reference to the original scene object in first and to the created miniature in second
    private List<(GameObject Original, GameObject Mini)> scenery; // list of tuples representing a reference to the original scene object in first and to the created miniature in second
    private List<(GameObject Original, GameObject Mini)> banner; // list of tuples representing a reference to the original scene object in first and to the created miniature in second
    
    public float scaleFactor = 666;
    public float playerUpscaling = 7;
    public float gain = 0.2f;
    private float translationDelay = 0.3f;
    
    private Quaternion playerRotationOffset;
    private Vector3 lastMiniPlayerPosition;

    private MonoBehaviour[] _activeStates;
    private IActiveState[] ActiveStates { get; set; }
    bool selectFlag = false;
    private float lastSwitch;
    private bool lookingAtMap = true;

    // Start is called before the first frame update
    void Start()
    {
        movables = new List<(GameObject Original, GameObject Mini)>();
        coins = new List<(GameObject Original, GameObject Mini)>();
        scenery = new List<(GameObject Original, GameObject Mini)>();
        banner = new List<(GameObject Original, GameObject Mini)>();


        playerModel = GameObject.FindGameObjectWithTag("PlayerModel");

        playerRotationOffset = Quaternion.Inverse(miniPlayer.transform.GetChild(0).GetChild(0).GetChild(1).rotation) * miniPlayer.transform.rotation;

        miniPlayer.transform.GetChild(0).GetChild(0).localScale = new Vector3(
                        playerModel.transform.lossyScale.x / miniPlayer.transform.GetChild(0).GetChild(0).GetChild(1).lossyScale.x,
                        playerModel.transform.lossyScale.y / miniPlayer.transform.GetChild(0).GetChild(0).GetChild(1).lossyScale.y,
                        playerModel.transform.lossyScale.z / miniPlayer.transform.GetChild(0).GetChild(0).GetChild(1).lossyScale.z)
                        / (scaleFactor / playerUpscaling);

        miniPlayer.transform.localRotation = playerRotationOffset * playerModel.transform.rotation;
        miniPlayer.transform.SetParent(this.transform, true);
        miniPlayer.transform.localPosition = playerModel.transform.position / scaleFactor;


        miniPlayer.transform.GetChild(0).GetChild(0).localPosition =
            Vector3.Scale(miniPlayer.transform.GetChild(0).GetChild(0).localPosition,
            miniPlayer.transform.GetChild(0).GetChild(0).localScale);

        // Reset pinch-hand to scaled location
        miniPlayer.transform.GetChild(1).localPosition = new Vector3(-0.112f, -0.0271f, -0.0499f);

        foreach (SkinnedMeshRenderer skin in playerModel.GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.enabled = false;

        _activeStates = miniPlayer.transform.gameObject.GetComponents<InteractorActiveState>();
        ActiveStates = _activeStates as IActiveState[];

        lastMiniPlayerPosition = miniPlayer.transform.position;

        // Do instatiation on copy of all objects
        ListUpdate(movables, "InteractibleMini");
        ListUpdate(coins, "coin");
        ListUpdate(scenery, "Scenery");
        ListUpdate(banner, "banner");
    }


    // Update is called once per frame
    void Update()
    {
        String currentStage = GameObject.FindWithTag("Player").GetComponent<LocomotionTechnique>().stage;
        GameObject targetT = GameObject.FindWithTag("Player").GetComponent<LocomotionTechnique>().selectionTaskMeasure.targerT;
        GameObject objectT = GameObject.FindWithTag("Player").GetComponent<LocomotionTechnique>().selectionTaskMeasure.objectT;
        lookingAtMap = 90 > Vector3.Angle(this.transform.up, playerEyes.position - this.transform.parent.position);

        // Next block is to keep mini map updated
        ListUpdate(movables, "InteractibleMini");
        ListUpdate(coins, "coin");
        ListUpdate(scenery, "Scenery");
        ListUpdate(banner, "banner");

        if (currentStage == "SecondBanner")
            if (targetT != null && targetT.transform.parent != this.transform)
            {
                targetT.transform.SetParent(this.transform, true);
                targetT.transform.localPosition = (targetT.transform.position - objectT.transform.position) / 2;
                targetT.transform.localPosition = new Vector3(targetT.transform.localPosition.x, System.Math.Abs(targetT.transform.localPosition.y), targetT.transform.localPosition.z);
            }

        if (currentStage == "FinalBanner")
            if (objectT != null && objectT.transform.parent != this.transform)
            {
                objectT.transform.SetParent(this.transform, true);
                objectT.transform.localPosition = (objectT.transform.position - targetT.transform.position) / 2;
                objectT.transform.localPosition = new Vector3(objectT.transform.localPosition.x, System.Math.Abs(objectT.transform.localPosition.y), objectT.transform.localPosition.z);
            }


        if (ActiveStates.Any(state => state.Active))
        {
            if (lastMiniPlayerPosition != miniPlayer.transform.position)
            {
                if (!selectFlag)
                {
                    selectFlag = true;
                    lastSwitch = Time.time;
                    playerModel.transform.parent.GetComponent<Rigidbody>().useGravity = false;
                }
                if (Time.time > lastSwitch + translationDelay)
                    playerModel.transform.parent.position = miniPlayer.transform.localPosition * scaleFactor;
                lastMiniPlayerPosition = miniPlayer.transform.position;
            }
        }
        else
        {
            if (selectFlag)
            {
                selectFlag = false;
                playerModel.transform.parent.GetComponent<Rigidbody>().useGravity = true;
            }
            miniPlayer.transform.localPosition = playerModel.transform.position / scaleFactor;
            miniPlayer.transform.localRotation = playerModel.transform.rotation * playerRotationOffset;
        }

        miniPlayer.transform.GetChild(0).GetChild(0).GetChild(1).GetChild(0).GetComponent<Renderer>().enabled = lookingAtMap;
        miniPlayer.transform.GetChild(0).GetChild(0).GetChild(1).GetChild(1).GetComponent<Renderer>().enabled = lookingAtMap;
    }


    void ListUpdate(List<(GameObject Original, GameObject Mini)> list, string tag)
    {
        foreach ((GameObject Original, GameObject Mini) goPair in list)
            if(goPair.Mini.GetComponent<Renderer>() != null)
                goPair.Mini.GetComponent<Renderer>().enabled = lookingAtMap;

        if (!lookingAtMap)
            return; // If player is not looking at the map, then we are done here.

        List<GameObject> originals = GameObject.FindGameObjectsWithTag(tag).ToList();

        if (originals.Count > list.Count) // Check whether any objects may be added
            foreach (GameObject go in originals)
                if (list.Find(x => x.Original.Equals(go)).Original == null) // filter out already added GameObjects
                {
                    // create a copy of every game object in movablesOriginal
                    GameObject mini = Instantiate(go, go.transform.position / scaleFactor, go.transform.rotation, this.transform);
                    mini.transform.localPosition = go.transform.position / scaleFactor;
                    mini.transform.localScale = go.transform.lossyScale / scaleFactor;
                    //mini.transform.rotation = Quaternion.Euler(0, mini.transform.rotation.eulerAngles.y, 0);
                    mini.transform.RotateAround(this.transform.localPosition, this.transform.localRotation.eulerAngles, this.transform.localRotation.eulerAngles.magnitude);
                    mini.tag = "Untagged";
                    mini.layer = 3;
                    foreach (Behaviour behaviour in mini.GetComponents<Behaviour>())
                        behaviour.enabled = false;
                    foreach (Collider collider in mini.GetComponents<Collider>())
                        collider.enabled = false;
                    foreach (MeshCollider collider in go.GetComponents<MeshCollider>())
                        collider.convex = true;
                    list.Add((go, mini));
                }

        if (originals.Count < list.Count)
            foreach ((GameObject Original, GameObject Mini) goPair in list)
                if (goPair.Original == null)
                {
                    Destroy(goPair.Mini);
                    list.Remove(goPair);
                }

        foreach ((GameObject Original, GameObject Mini) goPair in list)
            goPair.Mini.SetActive(goPair.Original.transform.parent.gameObject.activeSelf && goPair.Original.transform.gameObject.activeSelf);
    }
}