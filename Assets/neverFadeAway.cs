using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;

public class neverFadeAway : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public Renderer background;
    public Texture[] backgroundFrames;
    public KMSelectable[] cardButtons;
    public KMSelectable fortuneButton;
    public Renderer[] cardFaceRenderers;
    public Renderer fortuneRenderer;
    public Texture[] majorArcana;
    public Texture[] majorArcanaScrambled;
    public Texture[] fortuneTextures;

    private Coroutine[] cardGlitchingBaseCoroutines = new Coroutine[3];
    private Coroutine[] cardGlitchingEffectCoroutines = new Coroutine[3];
    private bool cantPress;
    private bool moduleSelected;

    private int[][] cardFaces;
    private int[] correctCards;
    private int[][] glitchFrequencies;
    private int[][] glitchDurations;
    private bool[][] allReversals;
    private int correctFortune;
    private int currentFortuneIx;
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
        fortuneButton.OnInteract += delegate () { PressFortune(); return false; };
        module.GetComponent<KMSelectable>().OnFocus += delegate () { moduleSelected = true; };
        module.GetComponent<KMSelectable>().OnDefocus += delegate () { moduleSelected = false; };
    }

    private void Start()
    {
        fortuneButton.gameObject.SetActive(false);
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
            cardGlitchingBaseCoroutines[i] = StartCoroutine(GlitchCard(i));
        }
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
        if (stage >= 3 || moduleSolved || cantPress)
            return;
        Debug.LogFormat("[Never Fade Away #{0}] Stage {1}: You pressed {2}.", moduleId, stage + 1, cardNames[cardFaces[stage][ix]]);
        audio.PlaySoundAtTransform("click", card.transform);
        if (ix == correctCards[stage])
        {
            stage++;
            Debug.LogFormat("[Never Fade Away #{0}] That was correct.{1}", moduleId, stage == 3 ? " Progressing to fortune submission." : " Progressing to stage " + (stage + 1) + ".");
            if (stage == 3)
            {
                var calcSequence = correctCards.Select(x => x + 1).ToArray();
                var ordinals = new string[] { "first", "second", "third", "fourth" };
                var unicorns = new string[][] { new string[] { "The Fool", "The High Priestess", "The Moon" }, new string[] { "Strength", "The Emperor", "The World" }, new string[] { "The Chariot", "The Lovers", "The Sun" }, new string[] { "The Hanged Man", "The Magician", "The Star" } };
                var correctCardnames = correctCards.Select((x, i) => new { value = x, index = i }).Select(x => cardNames[cardFaces[x.index][correctCards[x.index]]]).ToArray();
                if (unicorns.Any(unicorn => unicorn.SequenceEqual(correctCardnames.OrderBy(s => s))))
                {
                    correctFortune = Array.IndexOf(unicorns, unicorns.First(unicorn => unicorn.SequenceEqual(correctCardnames.OrderBy(s => s))));
                    Debug.LogFormat("[Never Fade Away #{0}] The selected cards match one of the listed spreads exactly. Select the {1} fortune.", moduleId, ordinals[correctFortune]);
                    return;
                }
                for (int i = 0; i < 3; i++)
                {
                    switch (correctCardnames[i])
                    {
                        case "The Fool":
                            break;
                        case "The Magician":
                            var tempMag = calcSequence[0];
                            calcSequence[0] = calcSequence[2];
                            calcSequence[2] = tempMag;
                            break;
                        case "The High Priestess":
                            var tempHP = calcSequence[0];
                            calcSequence[0] = calcSequence[1];
                            calcSequence[1] = tempHP;
                            break;
                        case "The Empress":
                            var tempEmprs = calcSequence[1];
                            calcSequence[1] = calcSequence[2];
                            calcSequence[2] = tempEmprs;
                            break;
                        case "The Emperor":
                            var tempEmpA = calcSequence[0];
                            var tempEmpB = calcSequence[1];
                            var tempEmpC = calcSequence[2];
                            calcSequence[0] = tempEmpB;
                            calcSequence[1] = tempEmpC;
                            calcSequence[2] = tempEmpA;
                            break;
                        case "The Hierophant":
                            var tempHieA = calcSequence[0];
                            var tempHieB = calcSequence[1];
                            var tempHieC = calcSequence[2];
                            calcSequence[0] = tempHieC;
                            calcSequence[1] = tempHieA;
                            calcSequence[2] = tempHieB;
                            break;
                        case "The Lovers":
                            calcSequence[0] = (calcSequence[0] + 1) % 3;
                            break;
                        case "The Chariot":
                            calcSequence[1] = (calcSequence[1] + 1) % 3;
                            break;
                        case "Strength":
                            calcSequence[2] = (calcSequence[2] + 1) % 3;
                            break;
                        case "The Hermit":
                            calcSequence[0] = (calcSequence[0] + 2) % 3;
                            break;
                        case "Wheel of Fortune":
                            calcSequence[0] = (calcSequence[1] + 2) % 3;
                            break;
                        case "Justice":
                            calcSequence[0] = (calcSequence[2] + 2) % 3;
                            break;
                        case "The Hanged Man":
                            calcSequence[0] = (calcSequence[0] + 1) % 3;
                            calcSequence[0] = (calcSequence[1] + 1) % 3;
                            calcSequence[0] = (calcSequence[2] + 1) % 3;
                            break;
                        case "Death":
                            calcSequence[0] = (calcSequence[0] + 2) % 3;
                            calcSequence[0] = (calcSequence[1] + 2) % 3;
                            calcSequence[0] = (calcSequence[2] + 2) % 3;
                            break;
                        case "Temperance":
                            calcSequence[0] = (calcSequence[1] + 1) % 3;
                            calcSequence[0] = (calcSequence[2] + 1) % 3;
                            break;
                        case "The Devil":
                            calcSequence[0] = (calcSequence[0] + 1) % 3;
                            calcSequence[0] = (calcSequence[2] + 1) % 3;
                            break;
                        case "The Tower":
                            calcSequence[0] = (calcSequence[0] + 1) % 3;
                            calcSequence[0] = (calcSequence[1] + 1) % 3;
                            break;
                        case "The Star":
                            calcSequence[0] = (calcSequence[1] + 2) % 3;
                            calcSequence[0] = (calcSequence[2] + 2) % 3;
                            break;
                        case "The Moon":
                            calcSequence[0] = (calcSequence[0] + 2) % 3;
                            calcSequence[0] = (calcSequence[2] + 2) % 3;
                            break;
                        case "The Sun:":
                            calcSequence[0] = (calcSequence[0] + 2) % 3;
                            calcSequence[0] = (calcSequence[1] + 2) % 3;
                            break;
                        case "Judgement":
                            calcSequence[1] = calcSequence[0];
                            break;
                        case "The World":
                            calcSequence[1] = calcSequence[2];
                            break;
                        default:
                            break;
                    }
                    Debug.LogFormat("[Never Fade Away #{0}] After applying the rule for {1}, the new sequence is {2}.", moduleId, cardNames[cardFaces[i][correctCards[i]]], calcSequence.Join(" "));
                }
                if (calcSequence[0] == calcSequence[1] && calcSequence[1] == calcSequence[2])
                {
                    calcSequence[1] = (calcSequence[1] + 1) % 3;
                    Debug.LogFormat("[Never Fade Away #{0}] All three digits of the final sequence are identical. Increment the middle one by 1 to get {1}.", moduleId, calcSequence.Join(", "));
                }
                var sequences = "321,112,213,113,121,212|313,311,131,132,123,232|332,223,331,231,322,312|221,133,122,233,323,211".Split('|').Select(x => x.Split(',')).ToArray();
                correctFortune = Array.IndexOf(sequences, sequences.First(arr => arr.Contains(calcSequence.Join(""))));
                Debug.LogFormat("[Never Fade Away #{0}] The final is associated with the {1} fortune. Select that one.", moduleId, ordinals[correctFortune]);
                StartCoroutine(SubmissionPhase());
            }
            else
                StartCoroutine(StageTransition());
        }
        else
        {
            Debug.LogFormat("[Never Fade Away #{0}] That was incorrect. Strike!", moduleId);
            module.HandleStrike();
        }
    }

    private void PressFortune()
    {
        if (stage < 3 || moduleSolved || cantPress)
            return;
        if (currentFortuneIx == correctFortune)
        {
            moduleSolved = true;
            module.HandlePass();
            audio.PlaySoundAtTransform("solve", transform);
            fortuneButton.gameObject.SetActive(false);
            Debug.LogFormat("[Never Fade Away #{0}] You selected the correct fortune. Module solved!", moduleId);
        }
        else
        {
            module.HandleStrike();
            Debug.LogFormat("[Never Fade Away #{0}] You selected the incorrect fortune. Strike!", moduleId);
        }
    }

    private IEnumerator GlitchCard(int ix)
    {
        yield return new WaitForSeconds(rnd.Range(.2f, .6f));
        var freqLowerBounds = new[] { 35, 20, 5 };
        var freqUpperBounds = new[] { 40, 25, 10 };
        var durLowerBounds = new[] { 30, 17, 5 };
        var durUpperBounds = new[] { 32, 20, 7 };
        var frequency = rnd.Range(freqLowerBounds[glitchFrequencies[stage][ix]], freqUpperBounds[glitchFrequencies[stage][ix]]);
        var duration = rnd.Range(durLowerBounds[glitchDurations[stage][ix]], durUpperBounds[glitchDurations[stage][ix]]);
        while (!cantPress)
        {
            cardGlitchingEffectCoroutines[ix] = StartCoroutine(GlitchEffect(cardFaceRenderers[ix], duration));
            yield return new WaitForSeconds(duration * .1f + frequency * .1f);
        }
    }

    private IEnumerator StageTransition()
    {
        cantPress = true;
        for (int i = 0; i < 3; i++)
        {
            StopCoroutine(cardGlitchingBaseCoroutines[i]);
            cardGlitchingBaseCoroutines[i] = null;
            StopCoroutine(cardGlitchingEffectCoroutines[i]);
            cardGlitchingEffectCoroutines[i] = null;
        }
        foreach (Renderer card in cardFaceRenderers)
            StartCoroutine(GlitchEffect(card, 2));
        yield return new WaitForSeconds(.201f);
        for (int i = 0; i < 3; i++)
        {
            cardFaceRenderers[i].material.mainTexture = majorArcana[cardFaces[stage][i]];
            cardFaceRenderers[i].transform.localEulerAngles = new Vector3(90f, allReversals[stage][i] ? 180f : 0f, 0f);
            cardGlitchingBaseCoroutines[i] = StartCoroutine(GlitchCard(i));
        }
        audio.PlaySoundAtTransform("stage transition", transform);
        cantPress = false;
    }

    private IEnumerator SubmissionPhase()
    {
        cantPress = true;
        for (int i = 0; i < 3; i++)
        {
            StopCoroutine(cardGlitchingBaseCoroutines[i]);
            cardGlitchingBaseCoroutines[i] = null;
            StopCoroutine(cardGlitchingEffectCoroutines[i]);
            cardGlitchingEffectCoroutines[i] = null;
        }
        foreach (Renderer card in cardFaceRenderers)
            StartCoroutine(GlitchEffect(card, 2));
        yield return new WaitForSeconds(.201f);
        foreach (Renderer card in cardFaceRenderers)
            card.gameObject.SetActive(false);
        yield return new WaitForSeconds(.01f);
        cantPress = false;
        fortuneRenderer.material.mainTexture = fortuneTextures[0];
        fortuneButton.gameObject.SetActive(true);
        while (!moduleSolved)
        {
            if (moduleSelected)
                audio.PlaySoundAtTransform("fortune change", transform);
            fortuneRenderer.material.mainTexture = fortuneTextures[currentFortuneIx];
            yield return new WaitForSeconds(1.5f);
            var elapsed = 0f;
            var duration = .5f;
            Vector2 textureOffset = new Vector2(rnd.Range(0f, 2f), rnd.Range(0f, 2f));
            Vector2 textureScale = new Vector2(rnd.Range(-2f, 2f), rnd.Range(-2f, 2f));
            while (elapsed < duration)
            {
                var sharpHitCurve = Mathf.Round(Mathf.Exp(-Mathf.Pow(elapsed - duration, 2) / (2f * Mathf.Pow(0.09319812f, 2))) * 10000) / 10000;
                fortuneRenderer.material.mainTextureScale = Vector2.Lerp(Vector2.one, textureScale, sharpHitCurve);
                fortuneRenderer.material.mainTextureOffset = Vector2.Lerp(Vector2.zero, textureOffset, sharpHitCurve);
                yield return null;
                elapsed += Time.deltaTime;
            }
            fortuneRenderer.material.mainTextureScale = Vector2.one;
            fortuneRenderer.material.mainTextureOffset = Vector2.zero;
            currentFortuneIx = (currentFortuneIx + 1) % 4;
        }
    }


    private IEnumerator GlitchEffect(Renderer card, int duration)
    {
        var face = cardFaces[stage == 3 ? stage - 1 : stage][Array.IndexOf(cardFaceRenderers, card)];
        var elapsed = 0;
        while (elapsed < duration)
        {
            card.material.mainTexture = majorArcanaScrambled[face * 10 + rnd.Range(0, 10)];
            yield return new WaitForSeconds(.1f);
            elapsed++;
        }
        card.material.mainTexture = majorArcana[face];
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
    /*
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
    */
}
