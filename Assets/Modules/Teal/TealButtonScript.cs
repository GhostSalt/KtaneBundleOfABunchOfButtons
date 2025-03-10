﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using SingleSelectablePack;
using TealButton;
using UnityEngine;

using Rnd = UnityEngine.Random;

public class TealButtonScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;
    public KMSelectable ButtonSelectable;
    public GameObject ButtonCap;
    public MeshRenderer[] LedLights;
    public Material[] LedMats;
    public TextMesh _buttonText;
    public TextMesh[] _mainText;
    public Color[] _textColors;

    private static readonly Dictionary<int, int[]> _ruleSeededTables = new Dictionary<int, int[]>();

    private static int _moduleIdCounter = 1;
    private int _moduleId, _lastTimerSeconds, _lightIndex;
    private readonly int[] _lightCycle = { 0, 1, 2, 3 };
    private readonly int[] _screenTextIxs = new int[3];
    private readonly int[] _solutions = new int[3];
    private bool _moduleSolved, _buttonHeld, _checkTap, _isStriking;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        // START RULE SEED
        var rnd = RuleSeedable.GetRNG();
        if (rnd.Seed != 1)
            Debug.LogFormat("[The Teal Button #{0}] Using rule seed: {1}.", _moduleId, rnd.Seed);
        for (var i = 0; i < 73; i++)
            rnd.Next(0, 2);

        var directions = (TealDirection[])Enum.GetValues(typeof(TealDirection));
        var direction = directions[rnd.Next(0, 4)];

        int[] pairPos = { 0, 1, 2, 3, 4, 5 };
        rnd.ShuffleFisherYates(pairPos);

        var letterTable = _ruleSeededTables.ContainsKey(rnd.Seed)
            ? _ruleSeededTables[rnd.Seed]
            : (_ruleSeededTables[rnd.Seed] = LatinSquare.Generate(rnd, 9));
        // END RULE SEED

        for (int i = 0; i < _screenTextIxs.Length; i++)
            _screenTextIxs[i] = Rnd.Range(0, 9);

        var snDigits = BombInfo.GetSerialNumber().Select(ch => (ch >= '0' && ch <= '9' ? ch - '0' : ch - 'A' + 10) % 9).ToArray();
        var buttonNum = Rnd.Range(0, 9);
        _buttonText.text = buttonNum.ToString();

        var snPairs = new int[6];
        for (int i = 0; i < snPairs.Length; i++)
            snPairs[i] = snDigits[pairPos[i]];
        for (int i = 0; i < _solutions.Length; i++)
            _solutions[i] = letterTable[
                ((snPairs[i * 2] + (direction == TealDirection.Left ? 9 - buttonNum : direction == TealDirection.Right ? buttonNum : 0)) % 9) +
                9 * ((snPairs[i * 2 + 1] + (direction == TealDirection.Up ? 9 - buttonNum : direction == TealDirection.Down ? buttonNum : 0)) % 9)];
        Debug.LogFormat("[The Teal Button #{0}] The number printed on the button is {1}.", _moduleId, buttonNum);
        Debug.LogFormat("[The Teal Button #{0}] The solution is {1}.", _moduleId, _solutions.Select(ch => (char)('A' + ch)).Join(", "));

        SetLights();
        SetText();

        ButtonSelectable.OnInteract += ButtonPress;
        ButtonSelectable.OnInteractEnded += ButtonRelease;
    }

    private bool ButtonPress()
    {
        StartCoroutine(AnimateButton(0f, -0.05f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        if (!_moduleSolved)
        {
            _checkTap = false;
            _buttonHeld = true;
        }
        return false;
    }
    private void ButtonRelease()
    {
        StartCoroutine(AnimateButton(-0.05f, 0f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
        if (!_moduleSolved && !_isStriking)
        {
            _buttonHeld = false;
            if (_checkTap)
                CheckAnswer();
            else
            {
                SetLights();
                ButtonTap(_lastTimerSeconds);
            }
        }
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

    private void SetLights()
    {
        _lightIndex++;
        for (int i = 0; i < _lightCycle.Length; i++)
        {
            if (i != _lightCycle[(_lightIndex + 1) % 4])
                LedLights[i].material = LedMats[0];
            else
                LedLights[i].material = LedMats[1];
        }

    }

    private void Update()
    {
        var seconds = (int)BombInfo.GetTime() % 3;
        if (seconds != _lastTimerSeconds)
        {
            _lastTimerSeconds = seconds;
            if (!_isStriking)
                SetTextColors(seconds);
            if (_buttonHeld)
                _checkTap = true;
        }
    }

    private void SetText()
    {
        for (int i = 0; i < _mainText.Length; i++)
            _mainText[i].text = ((char)('A' + _screenTextIxs[i])).ToString();
    }
    private void SetTextColors(int sec)
    {
        if (!_moduleSolved && !_isStriking)
        {
            for (int i = 0; i < _mainText.Length; i++)
            {
                if (i == sec)
                    _mainText[i].color = _textColors[3];
                else
                    _mainText[i].color = _textColors[2];
            }
        }
    }

    private void ButtonTap(int ix)
    {
        _screenTextIxs[ix] = (_screenTextIxs[ix] + _lightCycle[_lightIndex % 4] + 1) % 9;
        SetText();
    }

    private void CheckAnswer()
    {
        bool isSolve = true;
        for (int i = 0; i < _screenTextIxs.Length; i++)
            if (_solutions[i] != _screenTextIxs[i])
                isSolve = false;
        if (isSolve)
        {
            Module.HandlePass();
            _moduleSolved = true;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
            for (int i = 0; i < _screenTextIxs.Length; i++)
                _mainText[i].color = _textColors[1];
            for (int i = 0; i < LedLights.Length; i++)
                LedLights[i].material = LedMats[1];
            Debug.LogFormat("[The Teal Button #{0}] Submitted {1}. Module solved.", _moduleId, _screenTextIxs.Select(ch => (char)('A' + ch)).Join(", "));
        }
        else
            StartCoroutine(Strike());
    }

    private IEnumerator Strike()
    {
        Debug.LogFormat("[The Teal Button #{0}] Submitted {1}. Strike.", _moduleId, _screenTextIxs.Select(ch => (char)('A' + ch)).Join(", "));
        Module.HandleStrike();
        _isStriking = true;
        for (int i = 0; i < _screenTextIxs.Length; i++)
        {
            if (_screenTextIxs[i] != _solutions[i])
                _mainText[i].color = _textColors[0];
            else
                _mainText[i].color = _textColors[1];
        }
        yield return new WaitForSeconds(1.2f);
        _isStriking = false;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} tap 3 1 2 1 [tap button when letters in those positions are highlighted] | !{0} submit";
#pragma warning restore 414

    public IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*(submit|hold)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            var time = (int)BombInfo.GetTime() % 3;
            yield return ButtonSelectable;
            while ((int)BombInfo.GetTime() % 3 == time)
                yield return null;
            yield return ButtonSelectable;
            yield return new WaitForSeconds(.1f);
            yield break;
        }

        var m = Regex.Match(command, @"^\s*(?:tap|press|click)\s+([1-3 ]*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        yield return null;
        var slotsStr = m.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var slots = new int[slotsStr.Length];
        for (var i = 0; i < slotsStr.Length; i++)
            if (!int.TryParse(slotsStr[i], out slots[i]) || slots[i] < 1 || slots[i] > 3)
                yield break;

        foreach (var slot in slots)
        {
            while ((int)BombInfo.GetTime() % 3 != slot - 1)
                yield return null;
            ButtonSelectable.OnInteract();
            ButtonSelectable.OnInteractEnded();
            yield return new WaitForSeconds(.1f);
        }
    }

    public IEnumerator TwitchHandleForcedSolve()
    {
        while (!_moduleSolved)
        {
            var slot = (int)BombInfo.GetTime() % 3;
            if (_screenTextIxs[slot] != _solutions[slot])
            {
                ButtonSelectable.OnInteract();
                ButtonSelectable.OnInteractEnded();
                yield return new WaitForSeconds(.1f);
            }
            else
                yield return true;

            if (Enumerable.Range(0, 3).All(ix => _screenTextIxs[ix] == _solutions[ix]))
            {
                // All slots correct: time to submit
                var time = (int)BombInfo.GetTime() % 3;
                ButtonSelectable.OnInteract();
                while ((int)BombInfo.GetTime() % 3 == time)
                    yield return true;
                ButtonSelectable.OnInteractEnded();
            }
        }
    }
}
