﻿using UnityEngine;
using System.Collections;
using System;

public class Platinum : Atom {

	protected override float epsilon
	{
		get { return ((float)(1.0922 * Math.Pow(10, -19))); } // J
	}
	
	protected override float sigma
	{
		get { return 2.5394f; } // m=Angstroms for Unit
	}
	
	protected override float massamu
	{
		get { return 195.084f; } //amu
	}

	// Use this for initialization
	void Start () {
		Color moleculeColor = new Color(.898f, .8941f, 0.8863f, 1.0f);
		gameObject.renderer.material.color = moleculeColor;
	}
}
