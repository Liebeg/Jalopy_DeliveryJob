using DeliveryJob;
using JaLoader;
using SoftMasking.Samples;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIPopupList;
using Console = JaLoader.Console;

namespace DeliveryJob
{
    public class DeliveryJob : Mod
    {
        public override string ModID => "DeliveryJob";
        public override string ModName => "Delivery Job";
        public override string ModAuthor => "Leaxx";
        public override string ModDescription => "Adds a fun side activity involving picking up & dropping off boxes to various houses in other cities!";
        public override string ModVersion => "1.0";
        public override string GitHubLink => "https://github.com/Jalopy-Mods/DeliveryJob";
        public override WhenToInit WhenToInit => WhenToInit.InGame;
        public override List<(string, string, string)> Dependencies => new List<(string, string, string)>()
        {

        };

        public override bool UseAssets => true;

        private readonly List<MonoBehaviour> alreadyChanged = new List<MonoBehaviour>();

        private GameObject wallsPrefab;

        public readonly Dictionary<(string, int),  (GameObject, bool)> houses = new Dictionary<(string, int), (GameObject, bool)>();

        [SerializeField] public DeliveryBoxSave saveData = new DeliveryBoxSave();

        public GameObject worldSpaceCanvas;

        public override void EventsDeclaration()
        {
            base.EventsDeclaration();
            EventsManager.Instance.OnRouteGenerated += StartChangesDelay;
            EventsManager.Instance.OnCustomObjectsLoaded += LoadData;
            EventsManager.Instance.OnSave += SaveAll;
            EventsManager.Instance.OnNewGame += () =>
            {
                if (File.Exists(Path.Combine(Application.persistentDataPath, @"DeliveryJob\DeliveryBoxes.json")))
                    File.Delete(Path.Combine(Application.persistentDataPath, @"DeliveryJob\DeliveryBoxes.json"));

                saveData.Clear();
            };

            PrePopulateDictionary();
        }

        private void PrePopulateDictionary()
        {
            for (int i = 1; i <= 28; i++)
            {
                houses.Add(("Dresden", i), (null, false));
                houses.Add(("Sturovo", i), (null, false));
                houses.Add(("Letenye", i), (null, false));
                houses.Add(("Dubrovnik", i), (null, false));
                houses.Add(("M. Tarnovo", i), (null, false));
                houses.Add(("Istanbul", i), (null, false));
            }
        }

        private void StartChangesDelay(string startLocation, string endLocation, int distance)
        {
            Invoke("DoChanges", 5f);
        }

        public void DoChanges()
        {
            SetCorrectStatus();

            foreach (StoreC store in FindObjectsOfType<StoreC>())
            {
                if(alreadyChanged.Contains(store)) continue;

                if (store.name.Contains("PetrolStation"))
                    continue;

                var walls1 = Instantiate(wallsPrefab, store.transform.Find("HUB_Market"));
                var walls2 = Instantiate(wallsPrefab, store.transform.Find("HUB_Market"));
                walls1.transform.localPosition = new Vector3(-10.6f, 4f, 5.2f);
                walls2.transform.localPosition = new Vector3(-10.6f, 4f, -1f);

                walls1.transform.localScale = walls2.transform.localScale = new Vector3(100, 100, 100);
                walls1.transform.localEulerAngles = walls2.transform.localEulerAngles = new Vector3(90, -90, 0);
                walls1.GetComponent<MeshRenderer>().material = walls2.GetComponent<MeshRenderer>().material = ModHelper.Instance.defaultEngineMaterial;
                walls1.SetActive(true);
                walls2.SetActive(true);

                int n = 0;
                foreach(Transform door in store.transform)
                {
                    if(door.name == "DoorPlain_01")
                    {
                        if (n == 0 || n == 2)
                        {
                            door.gameObject.AddComponent<AudioSource>();
                            EnvironmentDoorsC script = door.GetChild(0).GetChild(0).GetComponent<EnvironmentDoorsC>();
                            script.isLocked = false;
                            script.xyzClosed = new Vector3(0, 90, 0);
                            script.xyzOpen = new Vector3(0, 190, 0);
                            door.GetChild(0).GetChild(0).gameObject.AddComponent<Animator>();
                        }

                        if (n == 0)
                        {
                            int r = UnityEngine.Random.Range(1, 6);

                            for (int i = 0; i < r; i++)
                                CreateDeliveryBox(door.transform);
                        }

                        if (n == 2)
                        {
                            Vector3 position = door.transform.position + new Vector3(-1.5f, 0f, 10);
                            position.y = 0.1f;
                            string city = GetCityFromCountryCode(store.transform.root.GetComponent<Hub_CitySpawnC>().countryHUBCode - 1);

                            if(houses[(city, 21)].Item1 != null)
                                Destroy(houses[(city, 21)].Item1);

                            houses[(city, 21)] =  (CreateDropoffZone(position, city, 21), houses[(city, 21)].Item2);
                        }
                        n++;
                    }
                }

                alreadyChanged.Add(store);
            }

            foreach (LaikaBuildingC dealership in FindObjectsOfType<LaikaBuildingC>())
            {
                if(alreadyChanged.Contains(dealership)) continue;

                string city = GetCityFromCountryCode(dealership.transform.root.GetComponent<Hub_CitySpawnC>().countryHUBCode - 1);

                var palletClone = Instantiate(dealership.transform.Find("Pallet_002"), dealership.transform, true);
                palletClone.transform.localPosition = new Vector3(11, 0, 3.9f);
                palletClone.transform.localEulerAngles = new Vector3(270, 55, 0);

                var bollardClone = Instantiate(dealership.transform.Find("Bollard_001"), dealership.transform, true);
                bollardClone.transform.localPosition = new Vector3(9.8f, 0.0272f, 3.8f);
                bollardClone.transform.localEulerAngles = new Vector3(0, 55, 0);

                bollardClone.SetParent(palletClone, true);

                CreateDropoffZone(Vector3.zero, city, 28, palletClone.gameObject);

                if (houses[(city, 28)].Item1 != null)
                    Destroy(houses[(city, 28)].Item1);

                houses[(city, 28)] = (palletClone.gameObject, houses[(city, 28)].Item2);
                alreadyChanged.Add(dealership);
            }

            foreach(MotelLogicC motel in FindObjectsOfType<MotelLogicC>())
            {
                if(alreadyChanged.Contains(motel)) continue;

                string city = GetCityFromCountryCode(motel.transform.root.GetComponent<Hub_CitySpawnC>().countryHUBCode - 1);

                for (int i = 0; i < motel.roomDoors.Length; i++)
                {
                    if (houses[(city, i + 22)].Item1 != null)
                        Destroy(houses[(city, i + 22)].Item1);

                    houses[(city, i + 22)] = (CreateDropoffZone(motel.roomDoors[i].transform.parent.parent.Find("CashLoc").position + new Vector3(2, 0, 0), city, i + 21 + 1), houses[(city, i + 22)].Item2);
                }

                alreadyChanged.Add(motel);
            }

            foreach(Hub_CitySpawnC city in FindObjectsOfType<Hub_CitySpawnC>())
            {
                if(GetToggleValue("DeliverToHouses") == false)
                {
                    alreadyChanged.Add(city);
                    continue;
                }

                if(alreadyChanged.Contains(city)) continue;

                string cityName = GetCityFromCountryCode(city.countryHUBCode - 1);

                int currentHouseNumber = 1;
                List<GameObject> buildings = new List<GameObject>();

                foreach(GameObject _building in city.buildings)
                {
                    Transform building = _building.transform;
                    if (building.GetChild(0).GetComponent<StoreC>() || building.GetChild(0).GetComponent<MotelParentC>() || building.GetChild(0).GetComponent<LaikaBuildingC>())
                        continue;

                    buildings.Add(building.GetChild(0).gameObject);
                }
                int i = 1;
                foreach (GameObject building in buildings)
                {
                    Transform transform = null;

                    if (building.transform.GetComponentInChildren<DoorKnockingC>() != null)
                        transform = building.transform.GetComponentInChildren<DoorKnockingC>().transform;
                    else if (building.transform.GetComponentInChildren<doorKnocking>() != null)
                        transform = building.transform.GetComponentInChildren<doorKnocking>().transform;
                    else
                        continue;

                    if (currentHouseNumber >= 15)
                        break;

                    if (houses[(cityName, currentHouseNumber)].Item1 != null)
                        Destroy(houses[(cityName, currentHouseNumber)].Item1);

                    houses[(cityName, currentHouseNumber)] = (CreateDropoffZone(transform.position + transform.right * 3 + transform.forward, cityName, currentHouseNumber, resetY: true), houses[(cityName, currentHouseNumber)].Item2);
                    currentHouseNumber++;
                    i++;
                }
                Console.LogDebug(i + " houses available in " + cityName);

                alreadyChanged.Add(city);
            }

            SetCorrectStatus();
        }

        private void SetCorrectStatus()
        {
            foreach (var house in houses.Values)
            {
                if (house.Item2 == false)
                {
                    if (house.Item1 != null)
                    {
                        house.Item1?.SetActive(false);
                        Destroy(house.Item1.GetComponent<DropoffZoneData>().floatingTextObject);
                    }
                    continue;
                }

                if(house.Item1 != null)
                {
                    house.Item1.SetActive(true);
                    var obj = CreateFloatingText(house.Item1.transform.position + Vector3.up * 2, house.Item1.GetComponent<DropoffZoneData>().houseNumber.ToString());
                    house.Item1.GetComponent<DropoffZoneData>().floatingTextObject = obj;
                }

            }
        }

        public string GetCityFromCountryCode(int code)
        {
            string toReturn = "";

            switch (code)
            {
                case 0:
                    toReturn = "Dresden";
                    break;

                case 1:
                    toReturn = "Sturovo";
                    break;

                case 2:
                    toReturn = "Letenye";
                    break;

                case 3:
                    toReturn = "Dubrovnik";
                    break;

                case 4:
                    toReturn = "M. Tarnovo";
                    break;

                case 5:
                    toReturn = "Istanbul";
                    break;
            }

            return toReturn;
        }

        private GameObject CreateDropoffZone(Vector3 position, string city, int houseNumber, GameObject objectToChange = null, bool resetY = false)
        {
            if(objectToChange != null)
            {
                var script = objectToChange.AddComponent<DropoffZoneData>();

                script.city = city;
                script.houseNumber = houseNumber;

                objectToChange.SetActive(false);

                return null;
            }

            var e = GameObject.CreatePrimitive(PrimitiveType.Plane);
            if (resetY)
                position.y -= 2.7f;
            e.transform.position = position;
            Material material = new Material(Shader.Find("Legacy Shaders/Diffuse"))
            {
                mainTexture = new Texture2D(1, 1),
                color = new Color(0, 1, 0, 0.7f)
            };
            e.GetComponent<MeshRenderer>().material = ModHelper.Instance.GetGlowMaterial(material);
            e.AddComponent<BoxCollider>();
            var data = e.AddComponent<DropoffZoneData>();

            data.city = city;
            data.houseNumber = houseNumber;

            e.SetActive(houses[(city, houseNumber)].Item2);

            return e;
        }

        private GameObject CreateFloatingText(Vector3 position, string text)
        {
            var obj = new GameObject("FloatingText");
            obj.AddComponent<RectTransform>();
            obj.transform.position = position;
            obj.AddComponent<Text>().text = text;
            obj.GetComponent<Text>().fontSize = 50;
            obj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 150);

            obj.AddComponent<FloatingNumber>();
            return obj;
        }

        private void AddBoxesToCustomObjectsManager()
        {
            GameObject[] boxes = new GameObject[3];

            boxes[0] = Instantiate(ModHelper.Instance.CardboardBoxSmall);
            boxes[1] = Instantiate(ModHelper.Instance.CardboardBoxMed);
            boxes[2] = Instantiate(ModHelper.Instance.CardboardBoxBig);

            foreach(GameObject box in boxes)
            {
                box.AddComponent<DeliveryBoxData>();
                box.transform.Find("Opener").gameObject.SetActive(false);
                box.GetComponent<ObjectPickupC>().objectID = -1;
                var customObjectInfo = box.AddComponent<CustomObjectInfo>();
                customObjectInfo.SpawnNoRegister = true;
                customObjectInfo.objName = "Delivery Box";
                var identif = box.AddComponent<ObjectIdentification>();
                //box.name = ModID + "_" + prefabName;
                identif.ModID = ModID;
                identif.ModName = ModName;
                identif.Author = ModAuthor;
                identif.Version = ModVersion;
                identif.HasReceivedBasicLogic = true;
            }

            CustomObjectsManager.Instance.RegisterObject(boxes[0], "DeliveryBoxSmall");
            CustomObjectsManager.Instance.RegisterObject(boxes[1], "DeliveryBoxMed");
            CustomObjectsManager.Instance.RegisterObject(boxes[2], "DeliveryBoxBig");
        }

        private void CreateDeliveryBox(Transform transform)
        {
            int r = UnityEngine.Random.Range(0, 3);
            int basePay = 0;
            int distanceModifier = 20 + UnityEngine.Random.Range(0, 10);

            GameObject box = null;
            switch (r)
            {
                case 0:
                    box = CustomObjectsManager.Instance.SpawnObject("DeliveryBoxSmall");
                    basePay = 10;
                    break;

                case 1:
                    box = CustomObjectsManager.Instance.SpawnObject("DeliveryBoxMed");
                    basePay = 20;
                    break;

                case 2:
                    box = CustomObjectsManager.Instance.SpawnObject("DeliveryBoxBig");
                    basePay = 30;
                    break;
            }
            basePay += UnityEngine.Random.Range(0, 10);

            box.transform.position = transform.position;
            box.transform.SetParent(transform, true);
            box.transform.position += new Vector3(-1.5f, 1f, 10);
            box.transform.SetParent(null, true);
            //box.transform.Find("Opener").gameObject.SetActive(false);
            var data = box.GetComponent<DeliveryBoxData>();
            var info = box.GetComponent<CustomObjectInfo>();
            //info.objRegistryName = "DeliveryBox";
            //info.objName = "Delivery Box";

            int i = 1;
            
            if(GetToggleValue("DeliverToHouses") == true)
                i = UnityEngine.Random.Range(1, 29);
            else
                i = UnityEngine.Random.Range(21, 29);

            string[] cities = new string[6] { "Dresden", "Sturovo", "Letenye", "Dubrovnik", "M. Tarnovo", "Istanbul" };
            string city = cities[UnityEngine.Random.Range(0, 6)];
            int distance = Math.Abs(Array.IndexOf(cities, GetCurrentCity()) - Array.IndexOf(cities, city));

            int payCheck = basePay + distance * distanceModifier;
            if (distance == 0)
                payCheck /= 2;

            if (distance == 0 && i == 21)
                i++;

            // make sure that the motel room is not used by the player already

            foreach(MotelLogicC motel in FindObjectsOfType<MotelLogicC>())
            {
                if (motel.roomNumber == i - 21)
                {
                    if (i == 26)
                        i--;
                    else
                        i++;
                }
            }

            // for reference:
            // 1 - 15 are houses (16 - 20 are not used)
            // 21 is the shop
            // 22 - 26 are the motel rooms
            // 27 is the motel reception
            // 28 is the dealership

            if (GetToggleValue("DeliverToHouses") == true)
            {
                switch (city)
                {
                    case "Dresden":
                        if (i > 15)
                            i = UnityEngine.Random.Range(1, 16);
                        break;

                    case "Sturovo":
                        if (i > 6)
                            i = UnityEngine.Random.Range(1, 7);
                        break;

                    // finish with all cities
                }
            }

            houses[(city, i)] = (houses[(city, i)].Item1, true);
            houses[(city, i)].Item1?.SetActive(true);

            SetCorrectStatus();

            data.pay = payCheck;
            data.houseNumber = i;
            data.city = city;

            string location = ConvertHouseNumberToName(i);
            info.objDescription = $"A package that needs to be delivered to:\n\n- {city}\n- {location}\n\n- Paycheck: {payCheck}";
            box.SetActive(true);
        }

        public static string ConvertHouseNumberToName(int i)
        {
            string toReturn = "";

            if (i == 21)
                toReturn = "Shop";
            else if (i >= 22 && i <= 26)
                toReturn = $"Motel Room {i - 21}";
            else if (i == 27)
                toReturn = "Motel Reception";
            else if (i == 28)
                toReturn = "Dealership";
            else
                toReturn = $"House {i}";

            return toReturn;
        }

        private string GetCurrentCity()
        {
            string toReturn = "";

            string[] info = Camera.main.transform.Find("MapHolder/Location").GetComponent<TextMesh>().text.Split(' ');
            if (info.Length > 0 && info[0] != "" && info[0] != string.Empty)
            {
                string start = info[0];
                toReturn = start;
                if (info[0] == "M.")
                    toReturn = "M. Tarnovo";
            }

            return toReturn;
        }

        public override void SettingsDeclaration()
        {
            base.SettingsDeclaration();

            InstantiateSettings();

            AddToggle("DeliverToHouses", "EXPERIMENTAL - Deliver to random houses", false);
        }

        public override void CustomObjectsRegistration()
        {
            base.CustomObjectsRegistration();
        }

        public override void OnEnable()
        {
            base.OnEnable();
        }

        public override void Awake()
        {
            base.Awake();
        }

        public override void Start()
        {
            base.Start();

            wallsPrefab = LoadAsset<GameObject>("walls", "walls", "", ".prefab");
            wallsPrefab = Instantiate(wallsPrefab);
            wallsPrefab.SetActive(false);

            CreateCanvas();

            AddBoxesToCustomObjectsManager();

            StartChangesDelay("", "", 0);
        }

        private void CreateCanvas()
        {
            worldSpaceCanvas = new GameObject("WorldSpaceCanvas");
            Canvas canvas = worldSpaceCanvas.AddComponent<Canvas>();
            RectTransform rectTransform = worldSpaceCanvas.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
            rectTransform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            worldSpaceCanvas.AddComponent<CanvasScaler>();
            worldSpaceCanvas.AddComponent<GraphicRaycaster>();
        }

        public override void Update()
        {
            base.Update();
        }

        public override void OnDisable()
        {
            base.OnDisable();
        }

        public void LoadData()
        {
            if(SceneManager.GetActiveScene().buildIndex != 3)
                return;

            saveData.Clear();

            if (File.Exists(Path.Combine(Application.persistentDataPath, @"DeliveryJob\DeliveryBoxes.json")))
                saveData = JsonUtility.FromJson<DeliveryBoxSave>(File.ReadAllText(Path.Combine(Application.persistentDataPath, @"DeliveryJob\DeliveryBoxes.json")));
            else
                return;

            if(saveData.Count == 0)
                return;

            foreach (ObjectPickupC obj in FindObjectsOfType<ObjectPickupC>())
            {
                if (obj.inventoryPlacedAt == null)
                    continue;

                foreach(var data in saveData)
                {
                    if (data.Key != obj.inventoryPlacedAt.localPosition)
                        continue;


                    var boxData = obj.transform.gameObject.GetComponent<DeliveryBoxData>();
                    var tuple = DeliveryBoxSave.StringToTuple(data.Value);

                    var info = obj.transform.GetComponent<CustomObjectInfo>();

                    info.objDescription = $"A package that needs to be delivered to:\n\n- {tuple.Item3}\n- {ConvertHouseNumberToName(tuple.Item2)}\n\n- Paycheck: {tuple.Item1}";

                    boxData.pay = tuple.Item1;
                    boxData.houseNumber = tuple.Item2;
                    boxData.city = tuple.Item3;

                    //duplicate houses dictionary
                    var copy = new Dictionary<(string, int), (GameObject, bool)>(houses);

                    foreach (var item in copy)
                    {
                        if (item.Key == (tuple.Item3, tuple.Item2))
                        {
                            houses[(tuple.Item3, tuple.Item2)] = (houses[(tuple.Item3, tuple.Item2)].Item1, true);
                            if(copy[(tuple.Item3, tuple.Item2)].Item1 != null)
                                houses[(tuple.Item3, tuple.Item2)].Item1.SetActive(true);
                        }
                    }
                }
            }
        }

        public void SaveAll()
        {
            if(saveData.Count == 0)
                return;

            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, @"DeliveryJob")))
                Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, @"DeliveryJob"));

            File.WriteAllText(Path.Combine(Application.persistentDataPath, @"DeliveryJob\DeliveryBoxes.json"), JsonUtility.ToJson(saveData));
        }
    }
}

public class FloatingNumber : MonoBehaviour
{
    private Transform mainCam;
    private Transform worldSpaceCanvas;

    private void Start()
    {
        mainCam = Camera.main.transform;
        worldSpaceCanvas = FindObjectOfType<DeliveryJob.DeliveryJob>().worldSpaceCanvas.transform;

        transform.SetParent(worldSpaceCanvas, true);
    }

    private void Update()
    {
        transform.rotation = Quaternion.LookRotation(transform.position - mainCam.position);
    }
}

[Serializable]
public class DeliveryBoxSave : SerializableDictionary<Vector3, string> 
{
    public static string TupleToString((int, int, string) tuple)
    {
        string arrayStr = "";

        arrayStr += tuple.Item1 + "|";
        arrayStr += tuple.Item2 + "|";
        arrayStr += tuple.Item3;

        return arrayStr;
    }

    public static (int, int, string) StringToTuple(string arrayStr)
    {
        string[] array = arrayStr.Split('|');

        return (int.Parse(array[0]), int.Parse(array[1]), array[2]);
    }
}

public class DeliveryBoxData : MonoBehaviour
{
    public int pay = 0;
    public int houseNumber = 0;
    public string city;

    // add saving & loading of this file to save the boxes data

    void Awake()
    {
        EventsManager.Instance.OnCustomObjectsSaved += Save;
    }

    private void Save()
    {
        var objPickupC = GetComponent<ObjectPickupC>();

        if(objPickupC == null)
            return;

        if(objPickupC.inventoryPlacedAt == null)
            return;

        var deliveryJob = FindObjectOfType<DeliveryJob.DeliveryJob>();

        deliveryJob?.saveData.Add(objPickupC.inventoryPlacedAt.localPosition, DeliveryBoxSave.TupleToString((pay, houseNumber, city)));
    }
}

public class DropoffZoneData : MonoBehaviour
{
    public int houseNumber = 0;
    public string city;
    public List<DeliveryBoxData> alreadyDeliveredBoxes = new List<DeliveryBoxData>();

    public GameObject floatingTextObject;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<DeliveryBoxData>())
        {
            if(collision.gameObject.GetComponent<DeliveryBoxData>().houseNumber != houseNumber || collision.gameObject.GetComponent<DeliveryBoxData>().city != city)
                return;

            if(alreadyDeliveredBoxes.Contains(collision.gameObject.GetComponent<DeliveryBoxData>()))
                return;

            collision.gameObject.GetComponent<ObjectPickupC>().glowMaterial = collision.gameObject.GetComponent<ObjectPickupC>().startMaterial;
            FindObjectOfType<WalletC>().TotalWealth += collision.gameObject.GetComponent<DeliveryBoxData>().pay;
            Destroy(collision.gameObject.GetComponent<DeliveryBoxData>());
            FindObjectOfType<WalletC>().UpdateWealth();
            Destroy(collision.gameObject.GetComponent<ObjectPickupC>());

            FindObjectOfType<DeliveryJob.DeliveryJob>().houses[(city, houseNumber)] = (FindObjectOfType<DeliveryJob.DeliveryJob>().houses[(city, houseNumber)].Item1, false);

            alreadyDeliveredBoxes.Add(collision.gameObject.GetComponent<DeliveryBoxData>());
        }
    }
}
