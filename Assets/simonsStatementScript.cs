using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KModkit; //gets edgework and stuff

public class simonsStatementScript : MonoBehaviour {

	public KMAudio audio; //audio script
	public KMBombInfo bomb; //bomb info script

    //for logging:
	static int moduleIdCounter = 1;
	int modId;
	private bool modSolved; //default is false

    public KMSelectable[] buttons;
	public MeshRenderer[] displayLEDs;
	public TextMesh symbol;
	public Color[] colors; //RGBY
	public Material[] clrMats;

    private bool colorblind;
	private bool doSequence;
	private int[][] Sequence;
	private bool[] truthSequence;
	private int[] solveSequence;
	private int[] lookUpValues;
	private bool inputReceived; // used for making sounds after first input
	private bool activated = false; //used so that inputs are only counted once the lights are on
	private int stage = 0;
	private int step = 0;
	private float waitTime = 2.5f;
	private readonly string[] gates = {"AND", "OR", "XOR", "NAND", "NOR", "XNOR", "IMP", "IMPBY"};
	private readonly string[] symbols = {"∧", "∨", "⊻", "|", "↓", "↔", "→", "←"};
	private readonly string[] clrNames = {"red", "green", "blue", "yellow"};
	private int gate; //gate between indicators
	private int clr1; //left indicator
	private int clr2; //right indicator

	private readonly int[] TableA ={
	3, 3, 2, 0, 0, 3, 0, 3, 2, 2,
	2, 2, 0, 1, 2, 3, 1, 3, 0, 0, 
	1, 3, 1, 3, 1, 3, 3, 1, 1, 3,
	1, 0, 0, 3, 1, 2, 2, 0, 2, 2,
	3, 1, 0, 2, 0, 2, 1, 3, 2, 3};
	private readonly int[] TableB ={
	1, 2, 0, 2, 2, 1, 3, 0, 1, 0,
	1, 1, 1, 0, 3, 1, 3, 0, 1, 1,
	0, 0, 0, 2, 2, 3, 3, 0, 2, 2,
	3, 1, 2, 0, 3, 0, 2, 3, 1, 3,
	2, 3, 0, 1, 3, 0, 1, 3, 2, 2};

    void Awake(){
		modId = moduleIdCounter++;

		for (int i = 0; i < 4; i++){
			KMSelectable temp = buttons[i];
			int help = i;
			buttons[i].OnInteract += delegate(){Press(temp, help); return false;};
		}
		
		colorblind = GetComponent<KMColorblindMode>().ColorblindModeActive;
	}

	void Press(KMSelectable button, int index){
		if (activated){
			Debug.LogFormat("[Simon's Statement #{0}] {1} has been pressed", modId, button.name);
			inputReceived = true;
			//shake
			button.AddInteractionPunch(1f);

			//turn on light
			StopAllCoroutines();
			for (int i = 0; i < 4; i++){
				buttons[i].GetComponent<Light>().enabled = false;
				buttons[i].GetComponent<Renderer>().material = clrMats[i];
			}
			StartCoroutine(GlowCoroutine(button, index));

			//play sound
			audio.PlaySoundAtTransform(clrNames[index], button.transform);

			//check whether correct
			if (!modSolved && button == buttons[solveSequence[step-1]]){
				waitTime = 2.5f;
				if (step == stage){
					Debug.LogFormat("[Simon's Statement #{0}] Stage {1} passed", modId, stage);
					if (stage == 5){
						GetComponent<KMBombModule>().HandlePass();
						modSolved = true;
						activated = false;
						doSequence = false;
						Debug.LogFormat("[Simon's Statement #{0}] Module solved", modId);
						StartCoroutine(SolveAnimation());
					}
					step = 0;
					stage++;
					waitTime = 0.5f;
				}
				step++;
			} else if (!modSolved){
				Debug.LogFormat("[Simon's Statement #{0}] Expected {1}, module struck & reset stage.", modId, clrNames[solveSequence[step-1]]);
				GetComponent<KMBombModule>().HandleStrike();
				step = 1;
			}
			//TODO MAYBE: make animation?
		}
	}

	private IEnumerator SolveAnimation(){
		float interval = 0.06f;
		yield return new WaitForSeconds(0.7f);
		//audio.PlaySoundAtTransform("win", transform);
		buttons[0].GetComponent<Renderer>().material = clrMats[4];
		buttons[0].GetComponent<Light>().enabled = true;
		audio.PlaySoundAtTransform("red", transform);
		yield return new WaitForSeconds(interval);
		buttons[2].GetComponent<Renderer>().material = clrMats[6];
		buttons[2].GetComponent<Light>().enabled = true;
		audio.PlaySoundAtTransform("blue", transform);
		yield return new WaitForSeconds(interval);
		buttons[1].GetComponent<Renderer>().material = clrMats[5];
		buttons[1].GetComponent<Light>().enabled = true;
		audio.PlaySoundAtTransform("green", transform);
		yield return new WaitForSeconds(interval);
		buttons[3].GetComponent<Renderer>().material = clrMats[7];
		buttons[3].GetComponent<Light>().enabled = true;
		audio.PlaySoundAtTransform("yellow", transform);
		yield return new WaitForSeconds(0.2f);
		buttons[0].GetComponent<Renderer>().material = clrMats[0];
		buttons[0].GetComponent<Light>().enabled = false;
		buttons[1].GetComponent<Renderer>().material = clrMats[1];
		buttons[1].GetComponent<Light>().enabled = false;
		buttons[2].GetComponent<Renderer>().material = clrMats[2];
		buttons[2].GetComponent<Light>().enabled = false;
		buttons[3].GetComponent<Renderer>().material = clrMats[3];
		buttons[3].GetComponent<Light>().enabled = false;
	}

	private IEnumerator GlowCoroutine(KMSelectable button, int index) {
		yield return new WaitForSeconds(0.05f);
		button.GetComponent<Light>().enabled = true;
		button.GetComponent<Renderer>().material = clrMats[index+4];
		yield return new WaitForSeconds(0.5f);
		button.GetComponent<Light>().enabled = false;
		button.GetComponent<Renderer>().material = clrMats[index];
		yield return new WaitForSeconds(waitTime);
		StartCoroutine(FlashSequenceCoroutine());
	}
	
	private IEnumerator FlashSequenceCoroutine(){
		int routinePosition = 0;

		while(doSequence && routinePosition < stage){
			if (inputReceived){
				//add sounds
				audio.PlaySoundAtTransform(clrNames[Sequence[routinePosition][0]], buttons[Sequence[routinePosition][0]].transform);
				audio.PlaySoundAtTransform(clrNames[Sequence[routinePosition][1]], buttons[Sequence[routinePosition][1]].transform);
			}
			buttons[Sequence[routinePosition][0]].GetComponent<Renderer>().material = clrMats[Sequence[routinePosition][0]+4];
			buttons[Sequence[routinePosition][0]].GetComponent<Light>().enabled = true;
			buttons[Sequence[routinePosition][1]].GetComponent<Renderer>().material = clrMats[Sequence[routinePosition][1]+4];
			buttons[Sequence[routinePosition][1]].GetComponent<Light>().enabled = true;
			yield return new WaitForSeconds(0.5f);
			buttons[Sequence[routinePosition][0]].GetComponent<Renderer>().material = clrMats[Sequence[routinePosition][0]];
			buttons[Sequence[routinePosition][0]].GetComponent<Light>().enabled = false;
			buttons[Sequence[routinePosition][1]].GetComponent<Renderer>().material = clrMats[Sequence[routinePosition][1]];
			buttons[Sequence[routinePosition][1]].GetComponent<Light>().enabled = false;
			yield return new WaitForSeconds(0.2f);
			routinePosition++;
		}

		yield return new WaitForSeconds(1.5f);
		StartCoroutine(FlashSequenceCoroutine());
	}

	void Start () {
		gate = UnityEngine.Random.Range(0,8);
		symbol.text = symbols[gate];

		clr1 = UnityEngine.Random.Range(0,4);
		clr2 = UnityEngine.Random.Range(0,3);
		clr2 += clr1 <= clr2 ? 1 : 0;

		displayLEDs[0].material.color = colors[clr1];
		displayLEDs[1].material.color = colors[clr2];

		Debug.LogFormat("[Simon's Statement #{0}] The expression is: {1} {2} {3}", modId, clrNames[clr1], gates[gate], clrNames[clr2]);

		stage = 1;
		step = 1;

		Sequence = new int[5][];
		for(int i = 0; i < 5; i++){
			Sequence[i] = new int[2];
			int h1 = UnityEngine.Random.Range(0,4);
			int h2 = UnityEngine.Random.Range(0,3);
			h2 += h1 <= h2 ? 1 : 0;

			Sequence[i][0] = h1;
			Sequence[i][1] = h2;
		}

		string temp = clrNames[Sequence[0][0]] + " & " + clrNames[Sequence[0][1]];
		for(int i = 1; i < 5; i++)
			temp += ", " + clrNames[Sequence[i][0]] + " & " + clrNames[Sequence[i][1]];

		Debug.LogFormat("[Simon's Statement #{0}] The entire flashing sequence is: {1}", modId, temp);

		//evaluates the expression
		evaluate();
		temp = "" + truthSequence[0];
		for(int i = 1; i < 5; i++)
			temp += ", " + truthSequence[i];
		
		Debug.LogFormat("[Simon's Statement #{0}] This results in this sequence of truth values: {1}", modId, temp);


		//calculate the correct presses
		int baseValue = 0;
		List<string> litInds = new List<string>(bomb.GetOnIndicators());
		string serialNumber = bomb.GetSerialNumber();
		foreach (char character in serialNumber){
			bool contained = false;
			foreach (string litInd in litInds)
				contained = contained || (litInd.IndexOf(character) >= 0);
			if (contained) baseValue += 2;
		}
		baseValue -= bomb.CountDuplicatePorts() * 4;
		baseValue += bomb.GetBatteryCount() * bomb.GetPortCount();

		lookUpValues = new int[5];

		for (int i = 0; i < 5; i++){
			int help = baseValue * (i+1);
			while (help < 0)
				help += 25;
			while (help > 49)
				help -= 25;
			
			lookUpValues[i] = help;
		}
		
		temp = "" + lookUpValues[0];
		for(int i = 1; i < 5; i++)
			temp += ", " + lookUpValues[i];

		Debug.LogFormat("[Simon's Statement #{0}] With a base value of {1} this creates look up values for the tables as follows: {2}", modId, baseValue, temp);

		solveSequence = new int[5];
		for (int i = 0; i < 5; i++)
			solveSequence[i] = truthSequence[i] ? TableA[lookUpValues[i]] : TableB[lookUpValues[i]];
		
		temp = "" + clrNames[solveSequence[0]];
		for(int i = 1; i < 5; i++)
			temp += ", " + clrNames[solveSequence[i]];

		Debug.LogFormat("[Simon's Statement #{0}] The entire solve sequence is going to be: {1}", modId, temp);

		if (colorblind)
			foreach (KMSelectable button in buttons)
				button.GetComponentInChildren<TextMesh>().color = new Color(0, 0, 0, 255);
		else
			foreach (KMSelectable button in buttons)
				button.GetComponentInChildren<TextMesh>().color = new Color(0, 0, 0, 0);

		doSequence = true;
        GetComponent<KMBombModule>().OnActivate += Flash;
	}

	void Flash(){
		StartCoroutine(FlashSequenceCoroutine());
		activated = true;
	}

	private void evaluate(){
		truthSequence = new bool[5];
		bool[] values = new bool[4];

		for (int i = 0; i < 5; i++){
			for (int j = 0; j < 4; j++) values[j] = false;
			values[Sequence[i][0]] = true;
			values[Sequence[i][1]] = true;
				switch (gate){
				case 0: // AND
					truthSequence[i] = values[clr1] && values[clr2];
					break;
				case 1: // OR
					truthSequence[i] = values[clr1] || values[clr2];
					break;
				case 2: // XOR
					truthSequence[i] = (values[clr1] && !values[clr2]) || (!values[clr1] && values[clr2]);
					break;
				case 3: // NAND
					truthSequence[i] = !(values[clr1] && values[clr2]);
					break;
				case 4: // NOR
					truthSequence[i] = !(values[clr1] || values[clr2]);
					break;
				case 5: // XNOR
					truthSequence[i] = (!values[clr1] && !values[clr2]) || (values[clr1] && values[clr2]);
					break;
				case 6: // IMP
					truthSequence[i] = !(values[clr1] && !values[clr2]);
					break;
				case 7: // IMPBY
					truthSequence[i] = !(!values[clr1] && values[clr2]);
					break;
			}
		}
	}

}