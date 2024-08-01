using System;
using System.IO;
using System.Linq;
// using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;

namespace Examples.Bowling
{
    public enum ScoreType
    {
        Open = 0,
        Spare = 1,
        Strike = 2,
    }

    public class Score
    {
        public static readonly int MaxScore = 10;
        public static readonly int MaxFrames = 10;
        private int bonus = 0;
        private bool in_bonus = false;

        public int TotalScore { get; private set; } = 0;

        public int CurrentFrame { get; private set; } = 0;
        public int CurrentRoll { get; private set; } = 0;

        public int _prevClearedPins = 0;
        private readonly List<Tuple<ScoreType, int>> _scoreByShots = new();
        private List<int> _immediateScore = new();
        public int sum_score { get; private set; } = 0;

        public void TakeNewRoll(int clearedPins)
        {
            sum_score += clearedPins;
            var score = clearedPins - _prevClearedPins;
            if (clearedPins >= MaxScore)
            {
                _prevClearedPins = 0;
            }
            else
            {
                _prevClearedPins = clearedPins;
            }
            // update current state
            if (score == MaxScore && CurrentRoll == 0) // strike
            {

                _scoreByShots.Add(Tuple.Create(ScoreType.Strike, 0));
                CurrentRoll = 2;
            }
            else
            {
                if (CurrentRoll == 1 && _immediateScore.Last() + score == MaxScore) // strike
                {
                    _scoreByShots.Add(Tuple.Create(ScoreType.Spare, 0));
                }
                else if (CurrentRoll == 1) // open
                {
                    _scoreByShots.Add(Tuple.Create(ScoreType.Open, score + _immediateScore.Last()));
                }
                else
                {
                    _scoreByShots.Add(Tuple.Create(ScoreType.Open, 0));
                }
                CurrentRoll++;
            }
            _immediateScore.Add(score);

            // update previous rolls
            var count = _scoreByShots.Count;

            if (count > 1 && _scoreByShots[count - 2].Item1 == ScoreType.Spare) //last roll was a spare, first roll
            {
                _scoreByShots[count - 2] = Tuple.Create(_scoreByShots[count - 2].Item1, 10 + score);
            }

            if (count > 2 && (_scoreByShots[count - 3].Item1 == ScoreType.Strike))
            {
                _scoreByShots[count - 3] = Tuple.Create(_scoreByShots[count - 2].Item1, 10 + score + _immediateScore[count - 2]);
            }

            // update total score
            TotalScore = _scoreByShots.Sum(p => p.Item2);
            bonus = Math.Max(0, bonus - 1);
        }

        public bool CheckFrameOver()
        {
            CurrentFrame++;
            _prevClearedPins = 0;
            return true;

            if (CurrentFrame == MaxFrames - 1)
            {
                if (CurrentRoll > 1)//finisehd
                {
                    if (_scoreByShots.Last().Item1 == ScoreType.Strike && in_bonus==false)
                    {
                        bonus += 2;
                        in_bonus = true;
                    }
                    else if (_scoreByShots.Last().Item1 == ScoreType.Spare && in_bonus==false)
                    {
                        bonus += 1;
                        in_bonus = true;
                    }
                    CurrentRoll = 0;
                    _prevClearedPins = 0;
                    CurrentFrame++;
                    return true;
                }
            }
            else
            {
                if (CurrentRoll > 1)
                {
                    CurrentRoll = 0;
                    _prevClearedPins = 0;
                    CurrentFrame++;
                    return true;
                }
            }

            return false;
        }

        public bool CheckEpisodeOver()
        {
            return CurrentFrame >= MaxFrames;
        }

        public void Reset()
        {
            TotalScore = 0;
            sum_score = 0;
            CurrentFrame = 0;
            CurrentRoll = 0;
            _prevClearedPins = 0;
            _scoreByShots.Clear();
            _immediateScore.Clear();
            bonus = 0;
            in_bonus = false;
        }

        public void Encode(BinaryWriter writer)
        {
            writer.Write(TotalScore);
            writer.Write((byte)CurrentFrame);
            writer.Write(sum_score);
        }

        public void Decode(BinaryReader reader)
        {
            TotalScore = reader.ReadInt32();
            CurrentFrame = reader.ReadByte();
            sum_score = reader.ReadInt32();
        }
    }
}
