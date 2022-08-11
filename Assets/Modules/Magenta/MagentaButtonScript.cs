using KModkit;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using BlueButtonLib;
using Rnd = UnityEngine.Random;
using System.Text.RegularExpressions;

public class MagentaButtonScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable ButtonSelectable;
    public GameObject ButtonCap;
    public MeshRenderer[] LEDs;
    public MeshRenderer ButtonCapMesh, ComponentBG;
    public Material[] ButtonMats;

    private Coroutine[] _runningCoroutines = new Coroutine[3];
    private static int _moduleIdCounter = 1;
    private int _moduleId, _direction, _currentRow, _currentColumn, _startingRow, _startingColumn, _prevDirection, _current, _currentLetter;
    private float _angle;
    private string[] _morse = new string[] { ".-", "-...", "-.-.", "-..", ".", "..-.", "--.", "....", "..", ".---", "-.-", ".-..", "--", "-.", "---", ".--.", "--.-", ".-.", "...", "-", "..-", "...-", ".--", "-..-", "-.--", "--.." };
    private string _input, _word, _wordOutput, _givenDirections, _concatMorseWord;
    private string _alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private string _grid = "ABCDEFGHIJKLMNOPQRSTUVWXY";
    private string _directions = "URDL";
    private bool[] _done = new bool[2];
    private bool _moduleSolved, _buttonHeld, _checkHold, _active, _isClockwise, _inputting;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        Calculate();
        ButtonSelectable.OnInteract += ButtonPress;
        ButtonSelectable.OnInteractEnded += ButtonRelease;
        Module.OnActivate += delegate { _runningCoroutines[0] = StartCoroutine(BlinkMorse(_wordOutput)); _runningCoroutines[1] = StartCoroutine(RotateButton(_givenDirections)); _active = true; };
    }

    private void Update()
    {
        while (_angle < 0 && !_isClockwise)
            _angle += 360;
        _angle %= 360;
        ButtonCap.transform.localEulerAngles = new Vector3(0, _angle, 0);
        if (_active && !_moduleSolved)
        {
            if (_angle >= 315 || _angle < 45)
                _direction = 0;
            else if (_angle % 360 >= 45 && _angle % 360 < 135)
                _direction = 1;
            else if (_angle % 360 >= 135 && _angle % 360 < 225)
                _direction = 2;
            else
                _direction = 3;
            for (int i = 0; i < 4; i++)
            {
                if (i == _direction)
                    LEDs[i].material.color = new Color32(255, 64, 255, 255);
                else
                    LEDs[i].material.color = new Color(0, 0, 0);
            }
            if (_prevDirection != _direction)
            {
                _prevDirection = _direction;
                if (_buttonHeld)
                    _checkHold = true;
            }
        }
    }

    private void Calculate()
    {
        Restart:
        _wordOutput = "";
        _word = NavyButtonData.Data[Rnd.Range(0, NavyButtonData.Data.Length)].Item1;
        while (_word.Contains('Z') || Regex.IsMatch(_word, @"(.)\1"))
            _word = NavyButtonData.Data[Rnd.Range(0, NavyButtonData.Data.Length)].Item1;
        for (int i = 0; i < Rnd.Range(4, 8); i++)
            _givenDirections += _directions[Rnd.Range(0, 4)];
        for (int i = 0; i < _word.Length; i++)
        {
            int row = _grid.IndexOf(_word[i]) / 5;
            int column = _grid.IndexOf(_word[i]) % 5;
            char cache = _grid[(row * 5) + column];
            for (int j = 0; j < _givenDirections.Length; j++)
            {
                switch (_givenDirections[j])
                {
                    case 'U':
                        row = (row + 1) % 5;
                        break;
                    case 'R':
                        column = (column + 4) % 5;
                        break;
                    case 'D':
                        row = (row + 4) % 5;
                        break;
                    default:
                        column = (column + 1) % 5;
                        break;
                }
            }
            if (_grid[(row * 5) + column] == cache)
                goto Restart;
            else
                _wordOutput += _grid[(row * 5) + column];
        }
        for (int i = 0; i < _word.Length; i++)
            _concatMorseWord += _morse[_alphabet.IndexOf(_word[i])];
        Debug.Log(_word);
        Debug.Log(_wordOutput);
        Debug.Log(_concatMorseWord);
    }

    private bool ButtonPress()
    {
        StartCoroutine(AnimateButton(0f, -0.05f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        _buttonHeld = true;
        return false;
    }

    private void ButtonRelease()
    {
        if (!_moduleSolved)
            Audio.PlaySoundAtTransform("MagentaButtonSnap", ButtonCap.transform);
        if (!_moduleSolved && !_inputting)
        {
            _inputting = true;
            StopCoroutine(_runningCoroutines[0]);
            StopCoroutine(_runningCoroutines[1]);
            _runningCoroutines[2] = StartCoroutine(RotateButtonInput());
        }
        StartCoroutine(AnimateButton(-0.05f, 0f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
        if (!_moduleSolved && _checkHold)
        {
            if (_concatMorseWord[_current] == '.')
            {
                Module.HandleStrike();
                _inputting = false;
                _currentRow = _startingRow;
                _currentColumn = _startingColumn;
                _current = 0;
                _currentLetter = 0;
                Debug.Log(_concatMorseWord[_current] + _checkHold.ToString());
                _runningCoroutines[0] = StartCoroutine(BlinkMorse(_wordOutput));
                _runningCoroutines[1] = StartCoroutine(RotateButton(_givenDirections));
                StopCoroutine(_runningCoroutines[2]);
            }
            else
            {
                Debug.Log(_direction);
                _current = (_current + 1) % _concatMorseWord.Length;
                switch (_direction)
                {
                    case 0:
                        _currentRow = (_currentRow + 4) % 5;
                        break;
                    case 1:
                        _currentColumn = (_currentColumn + 1) % 5;
                        break;
                    case 2:
                        _currentRow = (_currentRow + 1) % 5;
                        break;
                    default:
                        _currentColumn = (_currentColumn + 4) % 5;
                        break;
                }
                if (_grid[(_currentRow * 5) + _currentColumn] == _word[_currentLetter])
                    _currentLetter++;
                if (_currentLetter == _word.Length - 1)
                {
                    Module.HandlePass();
                    _moduleSolved = true;
                }
            }
        }
        else if (!_moduleSolved)
        {
            if (_concatMorseWord[_current] == '-')
            {
                Module.HandleStrike();
                _inputting = false;
                _currentRow = _startingRow;
                _currentColumn = _startingColumn;
                _current = 0;
                _currentLetter = 0;
                Debug.Log(_concatMorseWord[_current] + _checkHold.ToString());
                _runningCoroutines[0] = StartCoroutine(BlinkMorse(_wordOutput));
                _runningCoroutines[1] = StartCoroutine(RotateButton(_givenDirections));
                StopCoroutine(_runningCoroutines[2]);
            }
            else
            {
                Debug.Log(_direction);
                _current = (_current + 1) % _word.Length;
                switch (_direction)
                {
                    case 0:
                        _currentRow = (_currentRow + 4) % 5;
                        break;
                    case 1:
                        _currentColumn = (_currentColumn + 1) % 5;
                        break;
                    case 2:
                        _currentRow = (_currentRow + 1) % 5;
                        break;
                    default:
                        _currentColumn = (_currentColumn + 4) % 5;
                        break;
                }
            }
            if (_grid[(_currentRow * 5) + _currentColumn] == _word[_currentLetter])
            {
                _currentLetter++;
                Debug.Log(_word[_currentLetter]);
            }
            if (_currentLetter == _word.Length - 1)
            {
                Module.HandlePass();
                _moduleSolved = true;
            }
        }
        _checkHold = false;
        _buttonHeld = false;
    }

    private IEnumerator BlinkMorse(string input)
    {
        var speed = 0.75f;
        while (!_moduleSolved)
        {
            for (int i = 0; i < input.Length; i++)
            {
                var index = _alphabet.IndexOf(input[i]);
                for (int j = 0; j < _morse[index].Length; j++)
                {
                    var wait = 0.25f * speed;
                    ButtonCapMesh.material = ButtonMats[1];
                    if (_morse[index][j] == '-')
                        wait += 0.5f * speed;
                    while (wait > 0f)
                    {
                        yield return null;
                        wait -= Time.deltaTime;
                    }
                    ButtonCapMesh.material = ButtonMats[0];
                    wait = 0.25f * speed;
                    while (wait > 0f)
                    {
                        yield return null;
                        wait -= Time.deltaTime;
                    }
                }
                var wait2 = 0.75f * speed;
                while (wait2 > 0f)
                {
                    yield return null;
                    wait2 -= Time.deltaTime;
                }
            }
            var wait3 = 1.5f * speed;
            while (wait3 > 0f)
            {
                yield return null;
                wait3 -= Time.deltaTime;
            }
        }
        ButtonCapMesh.material = ButtonMats[0];
    }

    private IEnumerator AnimateButton(float a, float b)
    {
        var duration = 0.1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            ButtonCap.transform.localPosition = new Vector3(0f, Easing.InOutQuad(elapsed, a, b, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ButtonCap.transform.localPosition = new Vector3(0f, b, 0f);
    }

    private IEnumerator RotateButton(string input)
    {
        _isClockwise = true;
        while (true)
        {
            var speed = 125f;
            var toRotate = 60f;
            for (int i = 0; i < input.Length; i++)
            {
                toRotate = 60f;
                if (_isClockwise)
                {
                    while (toRotate > 0)
                    {
                        yield return null;
                        _angle += Time.deltaTime * speed;
                        toRotate -= Time.deltaTime * speed;
                    }
                    while (_angle - (_directions.IndexOf(input[i]) * 90) > 45f || _angle - (_directions.IndexOf(input[i]) * 90) < 0f)
                    {
                        yield return null;
                        _angle += Time.deltaTime * speed;
                    }
                    _isClockwise = false;
                }
                else
                {
                    while (toRotate > 0)
                    {
                        yield return null;
                        _angle -= Time.deltaTime * speed;
                        toRotate -= Time.deltaTime * speed;
                    }
                    while (Mathf.Abs(_directions.IndexOf(input[i]) * 90) - _angle > 45f || _angle - (_directions.IndexOf(input[i]) * 90) > 0f)
                    {
                        yield return null;
                        _angle -= Time.deltaTime * speed;
                    }
                    _isClockwise = true;
                }
                var wait = 0.1f;
                while (wait > 0)
                {
                    yield return null;
                    wait -= Time.deltaTime;
                }
            }
            toRotate = 405f;
            while (toRotate > 0)
            {
                yield return null;
                if (_isClockwise)
                    _angle += Time.deltaTime * speed;
                else
                    _angle -= Time.deltaTime * speed;
                toRotate -= Time.deltaTime * speed;
            }
        }
    }

    private IEnumerator RotateButtonInput()
    {
        _isClockwise = true;
        ButtonCapMesh.material = ButtonMats[0];
        var speed = 125f;
        while (!_moduleSolved)
        {
            yield return null;
            _angle += Time.deltaTime * speed;
        }
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "Use '!{0} hold tap' to hold the button over a timer tick, then tap it.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        string[] commandArray = command.Split(' ');
        yield return new NotImplementedException();
    }

    public IEnumerator TwitchHandleForcedSolve()
    {
        yield return new NotImplementedException();
    }
}
