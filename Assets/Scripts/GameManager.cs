﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


public class Values
{
	static Dictionary<string, string> values = new Dictionary<string, string>();

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
	private static GameManager instance = null;
	public static GameManager Instance{ get { return instance; } }

	[Header("Player")]
	public Player Player;

	[HideInInspector]
	public FightManager FightManager;

	[Header("UI References")]
	public Transform textPanel;
	public Transform buttonPanel;
	public Transform infoPanel;
	public Transform mapHidingPanel;
	public Text playerNameInfoText;
	public Text playerHpInfoText;
	public Text playerLevelInfoText;
	public Text playerXpInfoText;
	public Transform mapPanel;
	Dictionary<Vector2Int,Button> MapCells = new Dictionary<Vector2Int,Button>();
	private float cellWidth, cellHeight;

	private bool buttonsDisplayed = false;

	[Header("Prefabs")]
	public GameObject TextBoxPrefab;
	public GameObject DialogueBoxPrefab;
	public GameObject ButtonPrefab;
	public GameObject LocationPrefab;
	public GameObject MapCursorPrefab;


	[Header("Initial values")]
	public GameEvent StartingGameEvent;
	private GameEvent CurrentGameEvent;
	public Map StartingMap;
	private Map CurrentMap;
	public Vector2Int StartingLocation;
	private Vector2Int CurrentLocation;

	[Header("Debug")]
	public Enemy foe;
	

	void Start () {
		//Singleton
		if(instance == null)
			instance = this;
		else
			Destroy(this);

		ClearText();
		ClearButtons();
		ClearMapPanel();

		//Référencement des autres managers
		FightManager = GetComponent<FightManager>();

		//starting map
		GoToMap(StartingMap);

		GoToLocation(StartingLocation.x, StartingLocation.y);

		Player.Init();

		//PlayGameEvent(StartingGameEvent);

		FightManager.BeginFight(foe);

		UpdatePlayerInfo();

	}

	private void Update()
	{
		UpdatePlayerInfo();
		if (CurrentGameEvent != null && Input.GetButtonDown("Fire1"))
		{
			if (!buttonsDisplayed)
			{
				DisplayNextParagraphInGameEvent();
			}
		}
	}

	#region GameEvent handling
	public void PlayGameEvent(GameEvent ge)
	{
		if (ge == null) return;

		CurrentGameEvent = ge;
		CurrentGameEvent.Init();

		ClearText();
		ClearButtons();

		DisplayNextParagraphInGameEvent();
	}

	public void DisplayParagraph(Paragraph paragraph, UnityAction onNext = null)
	{
		if (paragraph == null) return;

		//instantiate text
		CreateText(paragraph.Text);

		//instantiate choices
		foreach (Choice choice in paragraph.choices)
		{
			if (!Condition.AreVerified(choice.conditions)) continue;

			CreateButton(choice.text, delegate{Operation.ApplyAll(choice.operations);}, onNext);
		}

		//apply operations
		paragraph.ApplyOperations();
	}

	public void DisplayNextParagraphInGameEvent()
	{
		if (CurrentGameEvent == null) throw new System.Exception("[GameManager] No current GameEvent. Cannot display next paragraph");

		Paragraph p = CurrentGameEvent.GetNextParagraph();
		if (p == null) //end of game event, back to map
		{
			CurrentGameEvent = null;
			ClearText();
			DisplayParagraph(CurrentMap[CurrentLocation.x, CurrentLocation.y].description);
			return;
		}

		DisplayParagraph(p, delegate { DisplayNextParagraphInGameEvent(); });
	}
	#endregion

	#region Map handling
	public void GoToMap(Map map)
	{
		CurrentMap = map;
		ClearMapPanel();

		cellWidth = LocationPrefab.GetComponent<RectTransform>().rect.width;
		cellHeight = LocationPrefab.GetComponent<RectTransform>().rect.height;

		mapPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(cellWidth*map.Width, cellHeight*map.Height);
		MapCells.Clear();
		
		for (int v = 0; v<map.Height; v++)
		{
			for(int u = 0; u<map.Width; u++)
			{
				if (map[u, v] != null)
				{
					GameObject go = Instantiate(LocationPrefab, mapPanel);
					go.transform.localPosition = new Vector3(cellWidth * u, -v * cellHeight, 0);
					int _u = u;
					int _v = v;
					go.GetComponent<Button>().onClick.AddListener(delegate { GoToLocation(_u,_v); });
					MapCells.Add(new Vector2Int(_u, _v), go.GetComponent<Button>());
				}
			}
		}
	}

	public void GoToLocation(int u, int v)
	{
		if(CurrentMap == null)
		{
			Debug.LogError($"[GameManager] Cannot move: no map provided");
			return;
		}
		
		Location location = CurrentMap[u, v];
		if(location == null)
		{
			Debug.LogError($"[GameManager] No location found at ({u},{v})");
			return;
		}

		MapCursorPrefab.transform.localPosition = new Vector2(u * cellWidth, -v* cellHeight);

		ClearText();
		GameEvent randomEvent = location.GetRandomEvent();
		if (randomEvent != null) PlayGameEvent(randomEvent);
		else
		{
			DisplayParagraph(location.description);
		}


		//set the possible destinations
		foreach(KeyValuePair<Vector2Int,Button> pair in MapCells)
		{
			pair.Value.interactable = false;
		}
		CurrentLocation = new Vector2Int(u, v);
		Button b;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.up, out b)) b.interactable = true;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.right, out b)) b.interactable = true;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.down, out b)) b.interactable = true;
		if (MapCells.TryGetValue(CurrentLocation + Vector2Int.left, out b)) b.interactable = true;

	}
	#endregion

	#region Scroll panel layout

	public void CreateButton(string content, params UnityAction[] onClick)
	{
		GameObject go = Instantiate(ButtonPrefab, buttonPanel);
		go.GetComponentInChildren<Text>().text = content;
		var button = go.GetComponent<Button>();
		button.onClick.AddListener(ClearButtons);
		foreach (UnityAction action in onClick)
		{
			if (action != null)
				button.onClick.AddListener(action);
		}
		

		buttonsDisplayed = true;
	}

	public void CreateText(string content)
	{
		GameObject textBox = Instantiate(TextBoxPrefab, textPanel);
		Text text = textBox.transform.Find("Panel/Line").GetComponent<Text>();
		if (text == null) throw new System.Exception("[GameManager] Cannot find Text component of TextBox prefab ");
		text.text = content;
	}

	public void ClearText()
	{
		ClearChilds(textPanel);
	}

	public void ClearButtons()
	{
		ClearChilds(buttonPanel);
		buttonsDisplayed = false;
	}

	public void ClearMapPanel()
	{
		MapCursorPrefab.transform.SetParent(null);
		ClearChilds(mapPanel);
		MapCursorPrefab.transform.SetParent(mapPanel);
	}

	static void ClearChilds(Transform t)
	{
		foreach (Transform child in t)
		{
			Destroy(child.gameObject);
		}
	}

	#endregion

	public void UpdatePlayerInfo()
	{
		if (Player == null) return;

		playerNameInfoText.text = Player.Name;
		playerHpInfoText.text = Player.Hp.ToString() + " / " + Player.MaxHp.ToString();
		playerLevelInfoText.text = "Lvl. " + Player.Level.ToString();
		playerXpInfoText.text = "XP : " + Player.Xp.ToString();

	}



}
