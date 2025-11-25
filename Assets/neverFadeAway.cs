using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;
using System.Runtime.InteropServices.ComTypes;

public class neverFadeAway : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public Renderer background;
    public Texture[] backgroundFrames;
    public KMSelectable[] cardButtons;
    public Renderer[] cardFaceRenderers;
    public Texture[] majorArcana;

    private int[][] cardFaces;
    private int[] correctCards;
    private int[][] glitchFrequencies;
    private int[][] glitchDurations;
    private bool[][] allReversals;
    private int stage;

    private static readonly string[] cardNames = new string[] { "The Fool", "The Magician", "The High Priestess", "The Empress", "The Emperor", "The Hierophant", "The Lovers", "The Chariot", "Strength", "The Hermit", "Wheel of Fortune", "Justice", "The Hanged Man", "Death", "Temperance", "The Devil", "The Tower", "The Star", "The Moon", "The Sun", "Judgement", "The World" };
    private static readonly string[][] cardRules = "SF,R1,R2;>SD,R3,<MD;MD,>MF,<FD;FD,<MD,<MF;>FF,<SD,<FF;R0,>FF,SD;R2,FF,>FD;MF,>SF,<SF;<MF,>MD,<MF;>MD,MD,MD;SD,SF,SF;<SD,FD,>SD;>SF,<MF,>SF;>FD,>FD,MF;FF,SD,>MD;<MD,R0,R3;R1,>SD,R0;<SF,MF,R1;<FD,R2,FD;<FF,<FF,>FF;R3,<SF,<SD;MF,<FD,FF".Split(';').Select(x => x.Split(',')).ToArray();
    private bool submissionStage;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable card in cardButtons)
            card.OnInteract += delegate () { PressCard(card); return false; };
    }

    private void Start()
    {
        StartCoroutine(BackgroundAnimation());
        var allRules = new string[3][];
        var attempts = 0;
    tryAgain:
        cardFaces = Enumerable.Repeat(Enumerable.Repeat(-1, 3).ToArray(), 3).ToArray();
        correctCards = Enumerable.Repeat(-1, 3).ToArray();
        glitchFrequencies = Enumerable.Repeat(Enumerable.Repeat(-1, 3).ToArray(), 3).ToArray();
        glitchDurations = Enumerable.Repeat(Enumerable.Repeat(-1, 3).ToArray(), 3).ToArray();
        allReversals = Enumerable.Repeat(Enumerable.Repeat(false, 3).ToArray(), 3).ToArray();
        for (int i = 0; i < 3; i++)
        {
            cardFaces[i] = Enumerable.Range(0, 22).ToList().Shuffle().Take(3).ToArray();
            glitchFrequencies[i] = Enumerable.Range(0, 3).ToList().Shuffle().ToArray();
            glitchDurations[i] = Enumerable.Range(0, 3).ToList().Shuffle().ToArray();
            allReversals[i] = Enumerable.Repeat(rnd.Range(0, 2) == 0 ? true : false, 3).ToArray();
            string[] rules;
            var thisStage = CheckSolution(i, cardFaces[i], glitchFrequencies[i], glitchDurations[i], allReversals[i], out rules);
            allRules[i] = rules.ToArray();
            if (thisStage.Count(x => x) == 1)
            {
                correctCards[i] = Array.IndexOf(thisStage, true);
                continue;
            }
            /*else if (!rules.Any(x => x.Contains("R")))
            {
                if (thisStage.Count(x => x) == 0)
                    allReversals[i][rnd.Range(0, 3)] = true;
                else
                    while (thisStage.Count(x => x) != 1)
                    {
                        var ix = rnd.Range(0, 3);
                        allReversals[i][ix] = !allReversals[i][ix];
                    }
            }*/
            else
            {
                attempts++;
                goto tryAgain;
            }
        }
        Debug.Log("Found a solution in " + attempts + " attempts.");
        allRules = allRules.Select(x => x.Select(xx => xx.Replace('<', '←').Replace('>', '→')).ToArray()).ToArray();
        var speedNames = new[] { "slow", "medium", "fast" };
        var ordinals = new[] { "first", "second", "third" };
        for (int i = 0; i < 3; i++)
        {
            Debug.LogFormat("[Never Fade Away #{0}] Stage {1}:", moduleId, i + 1);
            for (int j = 0; j < 3; j++)
                Debug.LogFormat("[Never Fade Away #{0}] The {1} card is {2}{3}. It's frequency is {4}, and it's duration is {5}. It's rule is {6}.", moduleId, ordinals[j], cardNames[cardFaces[i][j]], allReversals[i][j] ? " reversed" : "", speedNames[glitchFrequencies[i][j]], speedNames[glitchDurations[i][j]], allRules[i][j]);
            Debug.LogFormat("[Never Fade Away #{0}] Press {1}.", moduleId, cardNames[cardFaces[i][correctCards[i]]]);
        }
        for (int i = 0; i < 3; i++)
        {
            cardFaceRenderers[i].material.mainTexture = majorArcana[cardFaces[stage][i]];
            cardFaceRenderers[i].transform.localEulerAngles = new Vector3(90f, allReversals[stage][i] ? 180f : 0f, 0f);
        }
        // START GLITCHING COROUTINES
    }

    private bool[] CheckSolution(int stage, int[] faces, int[] frequencies, int[] durations, bool[] reversals, out string[] rules)
    {
        var thisStage = new bool[3];
        rules = new string[3];
        for (int i = 0; i < 3; i++)
        {
            string rule = cardRules[faces[i]][stage];
            rules[i] = rule;
            if (rule[0] == 'R')
                thisStage[i] = reversals.Count(x => x) == rule[1] - '0';
            else if (rule.Last() == 'F' && rule.Length == 2)
                thisStage[i] = frequencies[i] == "SMF".IndexOf(rule[0]);
            else if (rule.Last() == 'D' && rule.Length == 2)
                thisStage[i] = durations[i] == "SMF".IndexOf(rule[0]);
            else
            {
                var ix = (i + (rule[0] == '<' ? 2 : 1)) % 3;
                thisStage[i] = rule.Last() == 'F' ? frequencies[ix] == "SMF".IndexOf(rule[0]) : durations[ix] == "SMF".IndexOf(rule[0]);
            }
            if (reversals[i])
                thisStage[i] = !thisStage[i];
        }
        return thisStage;
    }

    private void PressCard(KMSelectable card)
    {
        var ix = Array.IndexOf(cardButtons, card);
        if (stage >= 3 || moduleSolved)
            return;
        Debug.LogFormat("[Never Fade Away #{0}] Stage {1}: You pressed {2}.", moduleId, stage + 1, cardNames[cardFaces[stage][ix]]);
        if (ix == correctCards[stage])
        {
            stage++;
            Debug.LogFormat("[Never Fade Away #{0}] That was correct.{1}", moduleId, stage == 3 ? " Progressing to fortune submission." : " Progressing to stage " + (stage + 1) + ".");
            if (stage == 3)
            {
                // PROCEED TO FORTUNE SUBMISSION
            }
            else
            {
                // STOP GLITCHING COROUTINES, GLITCH INTO NEW TEXTURES
                for (int i = 0; i < 3; i++)
                {
                    cardFaceRenderers[i].material.mainTexture = majorArcana[cardFaces[stage][i]];
                    cardFaceRenderers[i].transform.localEulerAngles = new Vector3(90f, allReversals[stage][i] ? 180f : 0f, 0f);
                }
            }
        }
        else
        {
            Debug.LogFormat("[Never Fade Away #{0}] That was incorrect. Strike!", moduleId);
            module.HandleStrike();
        }
    }

    private IEnumerator BackgroundAnimation()
    {
        var ix = 0;
        while (true)
        {
            yield return new WaitForSeconds(.1f);
            ix++; ix %= 28;
            background.material.mainTexture = backgroundFrames[ix];
        }
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} ";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        yield return null;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
    }
}
