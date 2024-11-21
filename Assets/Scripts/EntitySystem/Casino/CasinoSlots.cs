using UnityEngine;
using UnityEngine.UI;

using System;
using Cysharp.Threading.Tasks;
using TMPro;
using LitMotion;

using TheRavine.Base;
using TheRavine.Extensions;
public class CasinoSlots : MonoBehaviour
{
    private System.Threading.CancellationTokenSource _cts = new();
    public const byte heightCasino = 4, lengthCasino = 5;
    private int[,] slots = new int[heightCasino, lengthCasino];
    [SerializeField] private byte distance = 150, animationSpeed = 1, countOfSpins = 10, levelCounting = 1;
    [SerializeField] private bool isAutoPlay = false;
    [SerializeField] private SlotLine[] slotLines;
    [SerializeField] private CasinoSlotPref currentPref;
    [SerializeField] private TextMeshProUGUI winText, costText, betText, bonusText;
    [SerializeField] private int winCount, cost, betCount, numOfBonusGames, firstPlayBonus;
    [SerializeField] private UICasino uICasino;
    [SerializeField] private GameObject gamble, doubleWin, bonusGames, cardGame, x2hint, x3hint;
    [SerializeField] private Image gambleImage, gambleGameImage, hiddenImage;
    [SerializeField] private Sprite variant1, variant2, defaultVariant;
    private RectTransform[] slotsLinesTransforms = new RectTransform[heightCasino];
    private Image[,] slotsSprites = new Image[heightCasino, lengthCasino];
    private bool[,] slotsPainted = new bool[heightCasino, lengthCasino];
    private CalculateWin calculateWin;

    private void PaintSlot(int k, Color color)
    {
        slotsSprites[k / 10, k % 10].color = color;
        slotsPainted[k / 10, k % 10] = true; 
    }
    private void Awake() {
        calculateWin = new CalculateWin(currentPref, PaintSlot);

        uICasino.StartUI(currentPref);
        
        for(byte i = 0; i < heightCasino; i++)
        {
            for(byte j = 0; j < lengthCasino; j++)
            {
                slotsSprites[i, j] = slotLines[i].slots[j].GetComponent<Image>();
                slots[i, j] = RavineRandom.RangeInt(0, currentPref.slotCells.Length);
                slotsSprites[i, j].sprite = currentPref.slotCells[slots[i, j]].sprite;
            }
            slotsLinesTransforms[i] = slotLines[i].line.GetComponent<RectTransform>();
        }

        costText.text = "Account: " + cost;

        AwaitAutoSpin().Forget();
    }

    private bool isSpin = false;
    private async UniTaskVoid AwaitAutoSpin()
    {
        while (!DataStorage.sceneClose)
        {
            if(isSpin) await UniTask.Delay(1500, cancellationToken: _cts.Token);
            if(isAutoPlay && !isSpin)
            {
                isSpin = true;
                SpinSlots(ReloadAwaitingAutoSpin).Forget();
            }
            await UniTask.Delay(1000, cancellationToken: _cts.Token);
        }
    }

    private void ReloadAwaitingAutoSpin()
    {
        CalculateWin();
    }

    public void TryToSpin()
    {
        if(!isSpin)
        {
            isSpin = true;
            SpinSlots(CalculateWin).Forget();
        }
    }

    public void ChangeAutoMod()
    {
        isAutoPlay = !isAutoPlay;
    }

    public void ChangeLevelMod(string i)
    {
        levelCounting = Convert.ToByte(i);
        betText.text = "Bet: " + betCount * levelCounting * levelCounting;

        if(levelCounting == 1)
        {
            x2hint.SetActive(false);
            x3hint.SetActive(false);
        }
        else if(levelCounting == 2)
        {
            x2hint.SetActive(true);
            x3hint.SetActive(false);
        }
        else if(levelCounting == 3)
        {
            x2hint.SetActive(true);
            x3hint.SetActive(true);
        }
    }

    public void ChangeBetMod(string i)
    {
        betCount = Convert.ToInt32(i);;
        betText.text = "Bet: " + betCount * levelCounting * levelCounting;
        uICasino.FillTheHelp(currentPref, betCount / 10);
    }
    private void CalculateWin()
    {
        winCount = calculateWin.Calculate(slots, levelCounting, betCount, true);
        winText.text = "Win: " + winCount;
        numOfBonusGames = calculateWin.CheckBonusGames(slots);
        if(numOfBonusGames > 0)
        {
            bonusGames.SetActive(true);
            AwaitBonusGames().Forget();
        }
        else if(winCount > 0)
        {
            Gamble().Forget();
        }
        else
        {
            isSpin = false;
        }
    }

    private void CalculateBonusWin()
    {
        winCount += calculateWin.Calculate(slots, levelCounting, betCount);
        winText.text = "Win: " + winCount;
        numOfBonusGames += calculateWin.CheckBonusGames(slots);
        AwaitBonusGames().Forget();
    }

    private async UniTaskVoid AwaitBonusGames()
    {
        for(int i = 1; i < heightCasino; i++)
            for(int j = 0; j < lengthCasino; j++)
            {
                slotsSprites[i, j].color = Color.white;
                slotsPainted[i, j] = false;
            }
        if(numOfBonusGames < 1)
        {
            await UniTask.Delay(1000, cancellationToken: _cts.Token);
            bonusGames.SetActive(false);
            if(winCount > 0)
                Gamble().Forget();
            else
                isSpin = false;
            return;
        }

        bonusText.text = $"x{numOfBonusGames}";
        await UniTask.Delay(1000, cancellationToken: _cts.Token);
        numOfBonusGames--;
        SpinSlots(CalculateBonusWin).Forget();
    }

    private void AddToAccount()
    {
        for(int i = 1; i < heightCasino; i++)
            for(int j = 0; j < lengthCasino; j++)
            {
                slotsSprites[i, j].color = Color.white;
                slotsPainted[i, j] = false;
            }
        cost += winCount;
        costText.text = "Account: " + cost;
        isSpin = false;
    }

    private bool agree, disagree;
    private async UniTaskVoid Gamble()
    {
        gamble.SetActive(true);
        var value = 1f;
        LMotion.Create(1f, 0f, 3f).Bind(x => value = x);
        agree = false;
        disagree = false;

        while(value > 0.01f)
        {
            if(((int)(value * 10)) % 2 == 1)
            {
                for(int i = 1; i < heightCasino; i++)
                    for(int j = 0; j < lengthCasino; j++)
                        if(slotsPainted[i, j]) slotsSprites[i, j].color = new Color(slotsSprites[i, j].color.r, slotsSprites[i, j].color.g, slotsSprites[i, j].color.b, 0f);
            }
            if(((int)(value * 10)) % 2 == 0)
            {
                for(int i = 1; i < heightCasino; i++)
                    for(int j = 0; j < lengthCasino; j++)
                        if(slotsPainted[i, j]) slotsSprites[i, j].color = new Color(slotsSprites[i, j].color.r, slotsSprites[i, j].color.g, slotsSprites[i, j].color.b, 1f);
            }


            if(agree)
            {
                CardGame().Forget();
                break;
            }
            else if(disagree)
            {
                break;
            }
            gambleImage.fillAmount = value;
            await UniTask.Delay(10, cancellationToken: _cts.Token);
        }
        gamble.SetActive(false);
        
        if(!agree) AddToAccount();
    }
    public void Agree()
    {
        agree = true;
    }

    public void DisAgree()
    {
        disagree = true;
    }

    private int variant, answer;
    private async UniTaskVoid CardGame()
    {
        cardGame.SetActive(true);

        variant = RavineRandom.RangeInt(1, 3);
        hiddenImage.sprite = defaultVariant;

        await UniTask.Delay(200, cancellationToken: _cts.Token);

        var value = 1f;
        LMotion.Create(1f, 0f, 3f).Bind(x => value = x);
        agree = false;
        disagree = false;

        variant = RavineRandom.RangeInt(1, 3);
        hiddenImage.sprite = defaultVariant;
        answer = 0;

        while(value > 0.01f)
        {
            if(answer > 0)
            {
                if(answer == variant)
                {
                    if(answer == 1)
                    {
                        hiddenImage.sprite = variant1;
                    }
                    else
                    {
                        hiddenImage.sprite = variant2;
                    }
                    winCount *= 2;
                    winText.text = "Win: " + winCount;

                    await UniTask.Delay(200, cancellationToken: _cts.Token);

                    CardGame().Forget();
                    return;
                }
                else
                {
                    if(answer == 1)
                    {
                        hiddenImage.sprite = variant2;
                    }
                    else
                    {
                        hiddenImage.sprite = variant1;
                    }
                    winCount = 0;
                    winText.text = "Win: " + winCount;
                    break;
                }
            }
            gambleGameImage.fillAmount = value;
            await UniTask.Delay(10, cancellationToken: _cts.Token);
        }

        await UniTask.Delay(1000, cancellationToken: _cts.Token);
        AddToAccount();

        cardGame.SetActive(false);
    }

    public void Variant1()
    {
        answer = 1;
    }

    public void Variant2()
    {
        answer = 2;
    }
    private async UniTaskVoid SpinSlots(Action action)
    {
        if(numOfBonusGames < 1)
        {
            int curbet = betCount * levelCounting * levelCounting;
            if(cost - curbet < 0)
            {
                isSpin = false;
                return;
            }
            cost -= curbet;
        }
        costText.text = "Account: " + cost;

        for(byte k = 0; k < countOfSpins; k++)
        {
            for(byte i = 0; i < lengthCasino; i++)
            {
                slots[0, i] = RavineRandom.RangeInt(0, currentPref.slotCells.Length);
                slotsSprites[0, i].sprite = currentPref.slotCells[slots[0, i]].sprite;
            }

            if(firstPlayBonus > 0)
            {
                int firstPlayBonusCell = RavineRandom.RangeInt(0, lengthCasino);
                slots[0, firstPlayBonusCell] = 10;
                slotsSprites[0, firstPlayBonusCell].sprite = currentPref.slotCells[10].sprite;
                firstPlayBonus--;
            }

            byte currentDistance = 0;
            while(currentDistance < distance)
            {
                for(byte i = 0; i < heightCasino; i++)
                {
                    slotsLinesTransforms[i].anchoredPosition += animationSpeed * Vector2.down;
                }
                await UniTask.Delay(10, cancellationToken: _cts.Token);
                currentDistance += animationSpeed;
            }

            slotsLinesTransforms[heightCasino - 1].anchoredPosition = new Vector2(0, 60);

            (slotsLinesTransforms[0], slotsLinesTransforms[3]) = (slotsLinesTransforms[3], slotsLinesTransforms[0]);
            (slotsLinesTransforms[1], slotsLinesTransforms[3]) = (slotsLinesTransforms[3], slotsLinesTransforms[1]);
            (slotsLinesTransforms[3], slotsLinesTransforms[2]) = (slotsLinesTransforms[2], slotsLinesTransforms[3]);

            int[,] newSlots = new int[heightCasino, lengthCasino];
            Image[,] newSlotsSprites = new Image[heightCasino, lengthCasino];
            
            for(byte i = 0; i < heightCasino; i++)
            {
                for(byte j = 0; j < lengthCasino; j++)
                {
                    if(i == 0) 
                    {
                        newSlots[0, j] = slots[heightCasino - 1, j];
                        newSlotsSprites[0, j] = slotsSprites[heightCasino - 1, j];
                    }
                    else 
                    {
                        newSlots[i, j] = slots[i - 1, j];
                        newSlotsSprites[i, j] = slotsSprites[i - 1, j];
                    }
                }
            }

            slots = newSlots;
            slotsSprites = newSlotsSprites;
        }
        action?.Invoke();
    }

    private void OnDestroy() {
        _cts.Cancel();
    }
}

[System.Serializable]
public struct SlotLine
{
    public GameObject line;
    public GameObject[] slots;
}