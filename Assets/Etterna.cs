using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

public class Etterna : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Info;
    public KMSelectable Button;
    public TextMesh Text;
    public GameObject FilledControl;
    public MeshRenderer[] Arrow;
    public MeshRenderer Playfield;
    public Material[] ArrowMat, PlayfieldMat;

    private bool _lightsOn = false, _started = false, _isSolved = false;
    private readonly byte[] _color = new byte[4];
    private byte[] _correct = new byte[4];
    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;
    private float _cycle = 0;
    private readonly List<byte> _input = new List<byte>(0);
    private StringBuilder _builder = new StringBuilder();

    /// <summary>
    /// Update the bar at the top of the screen, handles easing.
    /// </summary>
    private void FixedUpdate()
    {
        FilledControl.gameObject.transform.localScale += new Vector3((_cycle / 33 - FilledControl.gameObject.transform.localScale.x) / 5, 0, 0);
    }

    /// <summary>
    /// Increase the cycle for 'FixedUpdate()'.
    /// </summary>
    private IEnumerator UpdateDisplay()
    {
        while (_cycle < 32)
        {
            //play metronome
            if (_cycle == 0)
                Audio.PlaySoundAtTransform("high", Module.transform);

            else
                Audio.PlaySoundAtTransform("low", Module.transform);

            //increase the cycle every time this is run, and display the message
            _cycle += 8;
            Text.text = "Get ready to calibrate! (Anacrusis: " + (_cycle / 8) + " / 4 beats.)";

            yield return new WaitForSeconds(0.444444f);
        }

        _cycle = 0;
        Audio.PlaySoundAtTransform("music", Module.transform);

        while (true)
        {
            //increase the cycle every time this is run, and display the message
            _cycle++;
            Text.text = "Calibrating... (" + _cycle + " / 32 beats recorded.)";

            //finished cycle
            if (_cycle == 33)
            {
                _started = false;
                Calculate();
                yield break;
            }

            yield return new WaitForSeconds(0.444444f);
        }
    }

    /// <summary>
    /// Code that runs when bomb is loading.
    /// </summary>
    private void Start()
    {
        Module.OnActivate += Activate;
        _moduleId = _moduleIdCounter++;

        //before the module activates, all arrows should be invisible
        for (int i = 0; i < Arrow.Length; i++)
            Arrow[i].transform.localScale = new Vector3(0, 0, 0);
    }

    /// <summary>
    /// Lights get turned on.
    /// </summary>
    void Activate()
    {
        //after the module activates, all arrows should be visible
        for (int i = 0; i < Arrow.Length; i++)
        {
            //left and right
            if (i == 0 || i == 3)
                Arrow[i].transform.localScale = new Vector3(0.02666666f, 0.02f, 0.015f);

            //down and up
            else
                Arrow[i].transform.localScale = new Vector3(0.015f, 0.02f, 0.02666666f);
        }

        _lightsOn = true;
        Init();
    }

    /// <summary>
    /// Initalising buttons.
    /// </summary>
    private void Awake()
    {
        Button.OnInteract += delegate ()
        {
            HandlePress();
            return false;
        };
    }

    /// <summary>
    /// Creates new arrows and logs answer.
    /// </summary>
    private void Init()
    {
        //generates arrows
        Generate();

        //gets answer and logs it
        _correct = GetAnswer();
        Debug.LogFormat("[Etterna #{0}] The expected sequence is: {1}, {2}, {3}, and {4}.", _moduleId, _correct[0], _correct[1], _correct[2], _correct[3]);

        //failsafe
        for (int i = 0; i < _correct.Length; i++)
            //if outside ranges 1-32 or not sorted, discard module
            if (!IsValid(_correct[i].ToString()) || _correct[i] > _correct[Mathf.Clamp(i + 1, 0, 3)])
            {
                Text.text = "There was an unexpected error, please view the logfile!";
                Debug.LogFormat("[Etterna #{0}] I am abandoning myself because I detected an unexpected value in correct[{1}] ({2}), please send this log to Emik! (@Emik#0001)", _moduleId, i, _correct[i]);
                Debug.LogFormat("[Etterna #{0}] Detected error in {1}", _moduleId, Info.GetFormattedTime());

                StartCoroutine("Solve");
                break;
            }
    }

    /// <summary>
    /// If first press, start sequence. Otherwise, log each press in a temporary list.
    /// </summary>
    private void HandlePress()
    {
        //sounds and punch effect
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Module.transform);
        Audio.PlaySoundAtTransform("start", Module.transform);
        Button.AddInteractionPunch();

        //lights off or is solved should end it here
        if (!_lightsOn || _isSolved)
            return;

        //clear the input array and start the sequence
        if (!_started) 
        {
            StartCoroutine(UpdateDisplay());
            _input.Clear();
            _started = true;
            return;
        }

        //add a clap sound and input log to '_input'
        _builder.Append(_cycle + ", ");
        Audio.PlaySoundAtTransform("clap", Module.transform);
        _input.Add((byte)_cycle);
    }

    /// <summary>
    /// Returns the correct answer. (Array is always size 4)
    /// </summary>
    /// <returns>The correct sequence that will solve this module.</returns>
    private byte[] GetAnswer()
    {
        _correct = new byte[4];
        byte index = 0;

        //runs through the entire '_colors' array
        for (int i = 0; i < _colors.Length; i++)
        {
            //if the color has been found, we know the answer to that arrow
            if (_color[index] == _colors[i])
            {
                _correct[index] = (byte)(i + 1);
                index++;
            }

            if (index == 4)
                break;
        }

        return _correct;
    }

    /// <summary>
    /// Solves the module.
    /// </summary>
    private IEnumerator Solve()
    {
        //solved!
        Playfield.material = PlayfieldMat[Mathf.Clamp(Info.GetStrikes(), 0, PlayfieldMat.Length - 1)];
        Audio.PlaySoundAtTransform("solve", Module.transform);
        _cycle = 33;
        _isSolved = true;

        for (int i = 0; i < Arrow.Length; i++)
        Arrow[i].gameObject.transform.localScale = new Vector3(0, 0, 0);

        yield return new WaitForSeconds(0.02f);
        Module.HandlePass();
    }

    /// <summary>
    /// Calculates the answer, striking/solving accordingly.
    /// </summary>
    private void Calculate()
    {
        //removes the last comma
        _builder.Remove(_builder.Length - 2, 2);
        Debug.LogFormat("[Etterna #{0}] Detected input during beats {1}.", _moduleId, _builder);

        //there should be 4 inputs
        if (_input.Count != 4)
        {
            Text.text = "Calibration failed! (Expected 4 inputs, recieved " + _input.Count + ".)";
            Debug.LogFormat("[Etterna #{0}] Strike! Incorrect number of inputs, expected 4, recieved {1}.", _moduleId, _input.Count);
            Audio.PlaySoundAtTransform("strike", Module.transform);
            _cycle = 0;

            Module.HandleStrike();
            return;
        }

        //makes sure each input is correct
        for (int i = 0; i < _correct.Length; i++)
            if (_input[i] != _correct[i])
            {
                Text.text = "Calibration failed! (Button press #" + (i + 1) + " was incorrect.)";
                Debug.LogFormat("[Etterna #{0}] Strike! Button press #{1} was incorrect, expected {2}, recieved {3}.", _moduleId, i + 1, _correct[i], _input[i]);
                Audio.PlaySoundAtTransform("strike", Module.transform);
                _cycle = 0;

                Module.HandleStrike();
                return;
            }

        //if the method gets here, solve the module
        Text.text = "Calibration succeeded!";
        Debug.LogFormat("[Etterna #{0}] Sequence was correct! Module solved!", _moduleId);
        StartCoroutine("Solve");
    }

    /// <summary>
    /// Sets the color of each arrow based on the '_colors' array.
    /// </summary>
    private void Generate()
    {
        //sets positions
        Dictionary<byte, float> z = new Dictionary<byte, float>(4) { { 0, 0.22f }, { 1, 0.1233333f }, { 2, 0.0266666f }, { 3, -0.07f } };
        for (int i = 0; i < Arrow.Length; i++)
        {
            byte rng;
            float pos;

            //pick random index from dictionary only if it exists
            do rng = (byte)Random.Range(0, 4);
            while (!z.TryGetValue(rng, out pos));

            //apply the position, remove it from dictionary so no 2 arrows can be the same position
            Arrow[i].transform.localPosition = new Vector3(Arrow[i].transform.localPosition.x, Arrow[i].transform.localPosition.y, pos);
            z.Remove(rng);
        }

        bool validArrow;
        byte index = 0, min = 0;
        //gets random indexes
        for (int i = 0; i < Arrow.Length; i++)
        {
            validArrow = false;
            //tries to generate number, if linear search succeeds then stop
            do
            {
                index = (byte)Random.Range(0, 8);
                for (int j = min; j < _colors.Length; j++)
                    if (_colors[j] == index)
                    {
                        min = (byte)(j + 1);
                        validArrow = true;
                        break;
                    }
            } while (!validArrow);

            //sets next index
            _color[i] = index;
        }

        //logging
        byte biggerThan;
        string[] colorString = new string[8] { "Red", "Blue", "Green", "Yellow", "Pink", "Orange", "Cyan", "Gray" };
        Debug.LogFormat("[Etterna #{0}] The colors are: {1}, {2}, {3}, and {4}.", _moduleId, colorString[_color[0]], colorString[_color[1]], colorString[_color[2]], colorString[_color[3]]);

        //assign each color according to the positions, basically SelectionSort
        for (int i = 0; i < Arrow.Length; i++)
        {
            biggerThan = 0;

            for (int j = 0; j < Arrow.Length; j++)
                if (Arrow[i].transform.localPosition.z > Arrow[j].transform.localPosition.z)
                    biggerThan++;

            Arrow[i].material = ArrowMat[_color[biggerThan]];
        }
    }

    //contains the entire list of colors, should not ever be changed!
    private static readonly byte[] _colors = new byte[32]
    {
        0, 7, 6, 5, 4, 7, 3, 7, 2, 5, 6, 7,
        1, 7, 6, 5, 2, 7, 3, 7, 4, 5, 6, 7,
        0, 7, 6, 5, 4, 7, 3, 7,
    };

    /// <summary>
    /// Determines whether the input from the TwitchPlays chat command is valid or not.
    /// </summary>
    /// <param name="par">The string from the user.</param>
    private bool IsValid(string par)
    {
        byte b;
        //0-255
        if (!byte.TryParse(par, out b))
            return false;

        //1-255
        if (b == 0)
            return false;

        //1-32
        return b <= 32;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} submit <#> <#> <#> <#> (Presses the screen during beat '#' | valid numbers are from 1-32 | example: !{0} submit 1 7 13 19";
#pragma warning restore 414

    /// <summary>
    /// TwitchPlays Compatibility, detects every chat message and clicks buttons accordingly.
    /// </summary>
    /// <param name="command">The twitch command made by the user.</param>
    IEnumerator ProcessTwitchCommand(string command)
    {
        //splits each command by spaces
        string[] buttonPress = command.Split(' ');

        //if command is formatted correctly
        if (Regex.IsMatch(buttonPress[0], @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;

            //less than 4 numbers
            if (buttonPress.Length < 5)
                yield return "sendtochaterror Not enough inputs! Exactly 4 numbers must be registered!";

            //more than 4 numbers
            else if (buttonPress.Length > 5)
                yield return "sendtochaterror Too many inputs! Exactly 4 numbers must be registered!";

            //if command has an invalid parameter
            else if (!IsValid(buttonPress.ElementAt(1)) || !IsValid(buttonPress.ElementAt(2)) || !IsValid(buttonPress.ElementAt(3)) || !IsValid(buttonPress.ElementAt(4)))
                yield return "sendtochaterror Invalid number! Only numbers 1-32 can be used for all button presses.";

            //if command is valid, push button accordingly
            else
            {
                //press screen to start the sequence
                Button.OnInteract();
                yield return new WaitForSeconds(0.444f * 3.5f);

                byte[] seq = new byte[4];
                    
                //press each button based on user input
                for (int i = 0; i < 4; i++)
                {
                    byte.TryParse(buttonPress[i + 1], out seq[i]);

                    for (int j = i - 1; j >= 0; j--)
                        seq[i] -= seq[j];

                    //0.44f = 22 frames = 1 cycle
                    yield return new WaitForSeconds(0.444f * seq[i]);
                    Button.OnInteract();
                }
            }
        }
    }

    /// <summary>
    /// Force the module to be solved in TwitchPlays
    /// </summary>
    IEnumerator TwitchHandleForcedSolve()
    {
        //pushes the screen once, waits until anacrusis is complete
        Debug.LogFormat("Activating AutoSolver...");
        Button.OnInteract();
        yield return new WaitForSeconds(0.444f * 3.5f);

        byte[] seq = new byte[4];

        //press each button based on the answer
        for (int i = 0; i < 4; i++)
        {
            byte.TryParse(_correct[i].ToString(), out seq[i]);

            for (int j = i - 1; j >= 0; j--)
                seq[i] -= seq[j];

            //0.44f = 22 frames = 1 cycle
            yield return new WaitForSeconds(0.444f * seq[i]);
            Button.OnInteract();
        }
    }
}