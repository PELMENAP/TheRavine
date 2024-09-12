using UnityEngine;
using System;


using TheRavine.Extensions;
public class CalculateWin
{
    private CasinoSlotPref pref;
    private Action<int, Color> paint;
    public CalculateWin(CasinoSlotPref _pref, Action<int, Color> _paint)
    {
        pref = _pref;
        paint = _paint;
    }
    public int Calculate(int[,] slots, byte level, int bet = 10, bool isPaint = false)
    {
        int sum = 0;
        if(level >= 1)
        {
            for(byte i = 1; i < CasinoSlots.heightCasino; i++)
            {
                int target = slots[i, 0];
                if(target == 11)
                {
                    if(slots[i, 1] == 11)
                        if(slots[i, 2] == 11)
                            if(slots[i, 3] == 11)
                                if(slots[i, 4] == 11)
                                {
                                    sum = 1000;
                                    break;
                                }
                    continue;
                }
                byte count = 1;

                if(target != 10)
                {
                    Color color = RavineRandom.RangeColor();
                    for(int j = 1; j < CasinoSlots.lengthCasino;j++)
                        if(slots[i, j] == target || slots[i, j] == 11)
                        {
                            if(isPaint) paint?.Invoke(i * 10 + j, color);
                            count++;
                        } 
                        else break;
                    
                    if(count > 1)
                    {
                        if(isPaint) paint?.Invoke(i * 10, color);
                        sum += pref.slotCells[target].costs[count - 2];
                    }
                }
            }
        }

        if(level >= 2)
        {
            for(byte i = 1; i < CasinoSlots.heightCasino - 1; i++)
            {
                int target = slots[i, 0];
                if(target == 11)
                {
                    if(slots[i + 1, 1] == 11)
                        if(slots[i, 2] == 11)
                            if(slots[i + 1, 3] == 11)
                                if(slots[i, 4] == 11)
                                {
                                    sum = 1000;
                                    break;
                                }
                    continue;
                }
                byte count = 1;

                if(target != 10)
                {
                    Color color = RavineRandom.RangeColor();
                    for(int j = 1; j < CasinoSlots.lengthCasino;j++)
                        if(slots[i + j % 2, j] == target || slots[i + j % 2, j] == 11)
                        {
                            if(isPaint) paint?.Invoke((i + j % 2) * 10 + j, color);
                            count++;
                        } 
                        else break;
                    
                    if(count > 1)
                    {
                        if(isPaint) paint?.Invoke(i * 10, color);
                        sum += pref.slotCells[target].costs[count - 2];
                    }
                }
            }

            for(byte i = 2; i < CasinoSlots.heightCasino; i++)
            {
                int target = slots[i, 0];
                if(target == 11)
                {
                    if(slots[i - 1, 1] == 11)
                        if(slots[i, 2] == 11)
                            if(slots[i - 1, 3] == 11)
                                if(slots[i, 4] == 11)
                                {
                                    sum = 1000;
                                    break;
                                }
                    continue;
                }
                byte count = 1;

                if(target != 10)
                {
                    Color color = RavineRandom.RangeColor();
                    for(int j = 1; j < CasinoSlots.lengthCasino; j++)
                        if(slots[i - j % 2, j] == target || slots[i - j % 2, j] == 11)
                        {
                            if(isPaint) paint?.Invoke((i - j % 2) * 10 + j, color);
                            count++;
                        } 
                        else break;
                    
                    if(count > 1)
                    {
                        if(isPaint) paint?.Invoke(i * 10, color);
                        sum += pref.slotCells[target].costs[count - 2];
                    }
                }
            }
        }

        if(level >= 3)
        {
            for(byte i = 1; i < CasinoSlots.heightCasino - 1; i++)
            {
                int target = slots[i, 0];
                if(target == 11)
                {
                    if(slots[i, 1] == 11)
                        if(slots[i + 1, 2] == 11)
                            if(slots[i, 3] == 11)
                                if(slots[i, 4] == 11)
                                {
                                    sum = 1000;
                                    break;
                                }
                    continue;
                }
                byte count = 1;

                if(target != 10)
                {
                    Color color = RavineRandom.RangeColor();
                    for(int j = 1; j < CasinoSlots.lengthCasino;j++)
                    {
                        if(j == 2)
                        {
                            if(slots[i + 1, j] == target || slots[i + 1, j] == 11)
                            {
                                if(isPaint) paint?.Invoke((i + 1) * 10 + j, color);
                                count++;
                            }
                            else break;
                            continue;
                        }
                        if(slots[i, j] == target || slots[i, j] == 11)
                        {
                            if(isPaint) paint?.Invoke(i * 10 + j, color);
                            count++;
                        } 
                        else break;
                    }
                    
                    if(count > 1)
                    {
                        if(isPaint) paint?.Invoke(i * 10, color);
                        sum += pref.slotCells[target].costs[count - 2];
                    }
                }
            }

            for(byte i = 2; i < CasinoSlots.heightCasino; i++)
            {
                int target = slots[i, 0];
                if(target == 11)
                {
                    if(slots[i, 1] == 11)
                        if(slots[i - 1, 2] == 11)
                            if(slots[i, 3] == 11)
                                if(slots[i, 4] == 11)
                                {
                                    sum = 1000;
                                    break;
                                }
                    continue;
                }
                byte count = 1;

                if(target != 10)
                {
                    Color color = RavineRandom.RangeColor();
                    for(int j = 1; j < CasinoSlots.lengthCasino;j++)
                    {
                        if(j == 2)
                        {
                            if(slots[i - 1, j] == target || slots[i - 1, j] == 11)
                            {
                                if(isPaint) paint?.Invoke((i - 1) * 10 + j, color);
                                count++;
                            }
                            else break;
                            continue;
                        }
                        if(slots[i, j] == target || slots[i, j] == 11)
                        {
                            if(isPaint) paint?.Invoke(i * 10 + j, color);
                            count++;
                        } 
                        else break;
                    }
                    
                    if(count > 1)
                    {
                        if(isPaint) paint?.Invoke(i * 10, color);
                        sum += pref.slotCells[target].costs[count - 2];
                    }
                }
            }
            
            int tergetM = slots[1, 0];

            if(tergetM == 11)
                if(slots[1, 1] == 11)
                    if(slots[2, 2] == 11)
                        if(slots[3, 3] == 11)
                            if(slots[3, 4] == 11)
                                sum = 1000;
            
            byte countM = 1;

            if(tergetM != 10)
            {
                Color color = RavineRandom.RangeColor();

                if(slots[1, 1] == tergetM || slots[1, 1] == 11)
                {
                    if(isPaint) paint?.Invoke(10 + 1, color);
                    countM++;
                }

                if(slots[2, 2] == tergetM || slots[2, 2] == 11)
                {
                    if(isPaint) paint?.Invoke(2 * 10 + 2, color);
                    countM++;
                }

                if(slots[3, 3] == tergetM || slots[3, 3] == 11)
                {
                    if(isPaint) paint?.Invoke(3 * 10 + 3, color);
                    countM++;
                }

                if(slots[3, 4] == tergetM || slots[3, 4] == 11)
                {
                    if(isPaint) paint?.Invoke(3 * 10 + 4, color);
                    countM++;
                }


                if(countM > 1)
                {
                    if(isPaint) paint?.Invoke(10, color);
                    sum += pref.slotCells[tergetM].costs[countM - 2];
                }
            }

            tergetM = slots[3, 0];

            if(tergetM == 11)
                if(slots[3, 1] == 11)
                    if(slots[2, 2] == 11)
                        if(slots[1, 3] == 11)
                            if(slots[1, 4] == 11)
                                sum = 1000;
            
            countM = 1;

            if(tergetM != 10)
            {
                Color color = RavineRandom.RangeColor();

                if(slots[3, 1] == tergetM || slots[3, 1] == 11)
                {
                    if(isPaint) paint?.Invoke(3 * 10 + 1, color);
                    countM++;
                }

                if(slots[2, 2] == tergetM || slots[2, 2] == 11)
                {
                    if(isPaint) paint?.Invoke(2 * 10 + 2, color);
                    countM++;
                }

                if(slots[1, 3] == tergetM || slots[1, 3] == 11)
                {
                    if(isPaint) paint?.Invoke(1 * 10 + 3, color);
                    countM++;
                }

                if(slots[1, 4] == tergetM || slots[1, 4] == 11)
                {
                    if(isPaint) paint?.Invoke(1 * 10 + 4, color);
                    countM++;
                }


                if(countM > 1)
                {
                    if(isPaint) paint?.Invoke(30, color);
                    sum += pref.slotCells[tergetM].costs[countM - 2];
                }
            }

        }

        sum *= bet / 10;

        return sum;
    }

    public int CheckBonusGames(int[,] slots)
    {
        int sum = 0;
        for(byte i = 1; i < CasinoSlots.heightCasino; i++)
        {
            int target = slots[i, 0];

            if(target == 11)
            {
                if(slots[i, 1] == 11)
                    if(slots[i, 2] == 11)
                        if(slots[i, 3] == 11)
                            if(slots[i, 4] == 11)
                            {
                                sum = 1000;
                                break;
                            }
                continue;
            }
            
            byte count = 1;

            if(target == 10)
            {
                Color color = RavineRandom.RangeColor();
                for(int j = 1; j < CasinoSlots.lengthCasino;j++)
                    if(slots[i, j] == target || slots[i, j] == 11)
                    {
                        paint?.Invoke(i * 10 + j, color);
                        count++;
                    }
                    else break;
                
                if(count > 1)
                {
                    paint?.Invoke(i * 10, color);
                    sum += pref.slotCells[target].costs[count - 2];
                }
            }
        }
        return sum;
    }
}
