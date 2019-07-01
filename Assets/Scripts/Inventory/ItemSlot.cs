﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{

	public static ItemSlot ItemSlotUnderPointer;

	public Image icon;
	public ItemSelectionOptionPanel optionPanel;

	public Item Item;

	public void SetItem(Item newItem)
	{
		Item = newItem;
		optionPanel.Item = Item;
		optionPanel.UpdateUI();

		if(Item != null)
		{
			icon.enabled = true;
			icon.sprite = Item.icon;
		}
		else
		{
			icon.enabled = false;
		}

	}

	public void OnPointerEnter()
	{
		if(Item != null)
		{
			InventoryUI.DescriptionPanel.Show(Item);

			optionPanel.gameObject.SetActive(true);
		}
		ItemSlotUnderPointer = this;
	}

	public void OnPointerExit()
	{
		InventoryUI.DescriptionPanel?.Hide();
		optionPanel.gameObject.SetActive(false);
		ItemSlotUnderPointer = null;
	}

	public void Swap(ItemSlot other)
	{
		if (this == other) return;

		Item formerItem = Item;
		SetItem(other.Item);
		other.SetItem(formerItem);
	}
}
