﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class Values
{
	public static Dictionary<string, string> values = new Dictionary<string, string>();

	static public bool ContainsKey(string key)
	{
		return values.ContainsKey(key);
	}

	static public bool GetValueAsFloat(string key, out float value)
	{
		string s;
		value = 0;
		if (!values.TryGetValue(key, out s)) return false;
		if (!float.TryParse(s, out value)) return false;
		return true;
	}

	static public void SetValueAsFloat(string key, float value)
	{
		if(values.ContainsKey(key))
		{
			values[key] = value.ToString();
		}
		else
		{
			values.Add(key, value.ToString());
		}
	}

	static public bool GetValueAsString(string key, out string value)
	{
		return values.TryGetValue(key, out value);
	}

	static public void SetValueAsString(string key, string value)
	{
		if (values.ContainsKey(key))
		{
			values[key] = value;
		}
		else
		{
			values.Add(key, value);
		}
	}

}

public class GameManager : MonoBehaviour {
	//Singleton instance
	public static GameManager Instance;
	private void Awake()
	{
		Instance = this;
	}

	public Team PlayerTeam;

	[Header("UI References")]
	public Transform Canvas;
	public Transform FrontCanvas;
	public Transform ScrollPanel;
	public Transform RightPanel;
	public Transform ContentPanel;
	public Transform TextPanel;
	public Transform ButtonPanel;
	public Transform infoPanel;
	public Transform mapHidingPanel;
	public TeamPanel TeamPanel;
	public Transform MapPanel;
	public GameObject CharacterWindow;
	public CharacterPanel CharacterPanel;

	Dictionary<Vector2Int,Button> MapCells = new Dictionary<Vector2Int,Button>();
	private float cellWidth, cellHeight;

	private int buttonsDisplayed = 0;

	[Header("Prefabs")]
	public GameObject TextBoxPrefab;
	public GameObject DialogueBoxPrefab;
	public GameObject ButtonPrefab;
	public GameObject LocationPrefab;
	public GameObject MapCursorPrefab;
	public GameObject ItemSlotPrefab;


	[Header("Initial values")]
	public GameEvent StartingGameEvent;
	[HideInInspector]
	public GameEvent CurrentGameEvent;
	private Coroutine CurrentGameEventRoutine;
	[HideInInspector]
	private bool onMap;
	public Map StartingMap;
	[HideInInspector]
	public Map CurrentMap;
	public Vector2Int StartingLocation;
	[HideInInspector]
	public Vector2Int CurrentLocation;

	[Header("Debug")]
	public Enemy foe;

	void Start () {

		//we need to instantiate these so that we don't modify the source scriptable object
		PlayerTeam = Instantiate(PlayerTeam);
		PlayerTeam.InstantiateUnits();

		TeamPanel.SetTeam(PlayerTeam);

		//starting map
		GoToMap(StartingMap);
		GoToLocation(StartingLocation,true);

		PlayGameEvent(StartingGameEvent);

	}

	//to be removed
	private void Update()
	{
		TeamPanel.UpdateSlots();
	}

	public void AddTeammate(Character character)
	{
		var instantiatedCharacter = Instantiate(character);
		instantiatedCharacter.InFightTeam = (PlayerTeam.Units.Count(unit => (unit as Character).InFightTeam) < 4);
		PlayerTeam.Add(instantiatedCharacter);
		TeamPanel.SetTeam(PlayerTeam);
	}

	#region Save/Load
	public SaveManager.SavedGame Save()
	{
		List<SaveManager.ValuePair> values = new List<SaveManager.ValuePair>();
		foreach(KeyValuePair<string, string> pair in Values.values)
		{
			values.Add(new SaveManager.ValuePair() { Key = pair.Key, Value = pair.Value });
		}

		System.Func<Object, string> GetTrimmedName = (obj) => obj ? obj.name.Substring(0, obj.name.Length - 7) : "";
		return new SaveManager.SavedGame()
		{
			PlayerTeam = PlayerTeam.Units,
			EquippedWeapons = PlayerTeam.Units.ConvertAll(unit => Inventory.Instance.Items.FindIndex(item => item == (unit as Character).Weapon)),
			Inventory = Inventory.Instance.Items.ConvertAll(item => GetTrimmedName(item)),
			Money = Inventory.Instance.Money,
			Map = CurrentMap.name,
			Location = CurrentLocation,
			Values = values,
		};
		
	}

	public void Load(SaveManager.SavedGame savedGame)
	{
		PlayerTeam = new Team() { Units = savedGame.PlayerTeam };
		Inventory.Instance.Items = savedGame.Inventory.ConvertAll(name => Instantiate(Resources.Load($"Project1/Items/{name}") as Item));
		Inventory.Instance.Money = savedGame.Money;
		for (int i = 0; i < PlayerTeam.Count; ++i)
		{
			(PlayerTeam[i] as Character).Equip(savedGame.EquippedWeapons[i] >= 0 ? Inventory.Instance.Items[savedGame.EquippedWeapons[i]] as Weapon : null);

		}
		TeamPanel.SetTeam(GameManager.Instance.PlayerTeam);

		
		Inventory.Instance.TidyItems();

		//close all windows
		Inventory.Instance.MerchantWindow.SetActive(false);
		Inventory.Instance.InventoryWindow.SetActive(false);
		CharacterWindow.SetActive(false);

		//restore values
		Values.values = new Dictionary<string, string>();
		foreach(var pair in savedGame.Values)
		{
			Values.values.Add(pair.Key, pair.Value);
		}

		GoToMap(Resources.Load($"ExampleGame/Maps/{savedGame.Map}") as Map);
		GoToLocation(savedGame.Location);
		PlayGameEvent(CurrentMap[savedGame.Location]);
	}
	#endregion

	#region GameEvent handling
	public void PlayGameEvent(GameEvent gameEvent)
	{
		if(CurrentGameEventRoutine != null) StopCoroutine(CurrentGameEventRoutine);

		ClearText();
		ClearButtons();
		LockMap = !(gameEvent is Location);
		LockSave = !(gameEvent is Location);

		CurrentGameEventRoutine = StartCoroutine(GameEventRoutine(gameEvent));
	}

	private IEnumerator GameEventRoutine(GameEvent gameEvent)
	{
		CurrentGameEvent = Instantiate(gameEvent);

		yield return CurrentGameEvent.DisplayParagraph();

		if (!(CurrentGameEvent is Location)) PlayGameEvent(CurrentMap[CurrentLocation]);
		else CurrentGameEvent = null;
	}

	public void PlayLocation(Location location)
	{
		Encounter encounter = location.EncounterTable?.GetEncounter();

		if (encounter != null)
			FightManager.Instance.BeginFight(encounter.EnemyTeam, null, encounter.Introduction);
		else
			PlayGameEvent(location);
	}
	#endregion

	#region Map handling
	public void GoToMap(Map map)
	{
		CurrentMap = map;
		ClearMap();

		cellWidth = LocationPrefab.GetComponent<RectTransform>().rect.width;
		cellHeight = LocationPrefab.GetComponent<RectTransform>().rect.height;

		MapPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(cellWidth*map.Width, cellHeight*map.Height);
		MapCells.Clear();
		
		for (int v = 0; v<map.Height; v++)
		{
			for(int u = 0; u<map.Width; u++)
			{
				Location location = map[new Vector2Int(u, v)];
				if (location != null)
				{
					GameObject go = Instantiate(LocationPrefab, MapPanel);
					go.transform.localPosition = new Vector3(cellWidth * u, -v * cellHeight, 0);
					Vector2Int pos = new Vector2Int(u, v);
					go.GetComponent<Button>().onClick.AddListener(
						delegate {
							GoToLocation(pos);
							PlayLocation(CurrentMap[pos]);
						});
					MapCells.Add(pos, go.GetComponent<Button>());

					go.GetComponent<Image>().color = location.Color;

					var icon = go.transform.GetChild(0).GetComponent<Image>();
					if (location.Icon)
					{
						icon.enabled = true;
						icon.sprite = location.Icon;
					}
					else
						icon.enabled = false;
					
				}
			}
		}
	}

	public void GoToLocation(Vector2Int pos, bool centerOnCursor = false)
	{
		if(CurrentMap == null)
		{
			Debug.LogError($"[GameManager] Cannot move: no map provided");
			return;
		}
		
		Location location = CurrentMap[pos];
		if(location == null)
		{
			Debug.LogError($"[GameManager] No location found at {pos}");
			return;
		}

		//update position on map
		MapCursorPrefab.transform.localPosition = new Vector2(pos.x * cellWidth, -pos.y* cellHeight);
		//center in on the map
		if(centerOnCursor) MapPanel.transform.localPosition = -MapCursorPrefab.transform.localPosition + new Vector3(110,110);

		foreach(KeyValuePair<Vector2Int,Button> pair in MapCells)
		{
			pair.Value.interactable = false;
		}
		CurrentLocation = pos;
		Button b;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.up, out b)) b.interactable = true;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.right, out b)) b.interactable = true;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.down, out b)) b.interactable = true;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.left, out b)) b.interactable = true;

	}
	#endregion

	#region Panels layout

	public Button CreateButton(string content, params UnityAction[] onClick)
	{
		GameObject go = Instantiate(ButtonPrefab, ButtonPanel);
		go.GetComponentInChildren<Text>().text = content;
		var button = go.GetComponent<Button>();
		button.onClick.AddListener(ClearButtons);
		foreach (UnityAction action in onClick)
		{
			if (action != null)
				button.onClick.AddListener(action);
		} 
		buttonsDisplayed++;
		RefreshContent();
		return button;
	}

	public void CreateText(string content)
	{
		GameObject textBox = Instantiate(TextBoxPrefab, TextPanel);
		Text text = textBox.GetComponentInChildren<Text>();
		if (text == null) throw new System.Exception("[GameManager] Cannot find Text component of TextBox prefab ");
		text.text = content;
		RefreshContent();
	}

	public void ClearText()
	{
		ClearChilds(TextPanel);
		RefreshContent();
	}

	public void ClearButtons()
	{
		ClearChilds(ButtonPanel);
		buttonsDisplayed = 0;
		RefreshContent();
	}

	public void ClearMap()
	{
		MapCursorPrefab.transform.SetParent(null);
		ClearChilds(MapPanel);
		MapCursorPrefab.transform.SetParent(MapPanel);
	}


	public bool LockMap
	{
		set => mapHidingPanel.gameObject.SetActive(value);
	}

	public bool LockInventory
	{
		set
		{
			if (value == true) Inventory.Instance.InventoryWindow.gameObject.SetActive(false);
			Inventory.Instance.InventoryButton.interactable = !value;
		}
	}

	public bool LockSave
	{
		set
		{
			if (value == true) SaveManager.Instance.SaveWindow.gameObject.SetActive(false);
			SaveManager.Instance.SaveButton.interactable = !value;
		}
	}

	public bool LockAbilities
	{
		set => CharacterPanel.AbilityMaskingPanel.SetActive(value);
	}

	public bool LockCharacterSwap
	{
		set => CharacterPanel.LockCharacterSwap = value;
	}

	static void ClearChilds(Transform t)
	{
		foreach (Transform child in t)
		{
			Destroy(child.gameObject);
		}
	}

	//This is the only way to force update the layout groups

	bool willRefreshContent = false;
	public void RefreshContent()
	{
		if (!willRefreshContent)
			StartCoroutine(nameof(RefreshContentCoroutine));
	}

	public IEnumerator RefreshContentCoroutine()
	{
		willRefreshContent = true;

		ContentPanel.localScale = Vector3.zero;
		yield return null;
		LayoutRebuilder.ForceRebuildLayoutImmediate(ContentPanel.GetComponent<RectTransform>());
		LayoutRebuilder.ForceRebuildLayoutImmediate(TextPanel.GetComponent<RectTransform>());
		LayoutRebuilder.ForceRebuildLayoutImmediate(ButtonPanel.GetComponent<RectTransform>());
		ContentPanel.localScale = Vector3.one;

		willRefreshContent = false;
	}

	#endregion

}
