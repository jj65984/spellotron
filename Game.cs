using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace Spellotron
{
    /// <summary>
    /// Represents the game's logic. The Model in the MVC paradigm.
    /// </summary>
    class Game
    {
        //Fields
        Controller ctrl;
        string currentWord;           //The currently selected goal word
        char[] currentLetters;        //An array of the goal words letters (in order)
        char currentChar;             //The current goal character
        int currentCharIndex;         //Current goal character's index in currentLetters[]
        int score = 0;                //The user's score while performing the goal word
        double pointsPerCharacter;    //Maximum number of points each character can be worth
        DateTime characterStartTime;

        //Accessors for private fields
        public string CurrentWord { get { return currentWord; } }
        public char[] CurrentLetters { get { return currentLetters; } }
        public int CurrentCharIndex { get { return currentCharIndex; } }
        public int Score { get { return score; } }
        public double CharGoalTime { get { return charGoalTime; } }

        //Constants
        public static readonly double maxScore = 1234567; //Maximum possible score
        public static readonly double charGoalTime = 21.5;  //Time to hit character pose and still get points

        //Constructor
        public Game(Controller c)
        {           
            ctrl = c;
            ctrl.WriteToDebugWindow("Game created");
            StartNewWord();
        }

        /// <summary>
        /// Finds a new goal word and has the controller set it up in the view.
        /// Prepares the game logic for a new word;
        /// </summary>
        public void StartNewWord()
        {
            ctrl.CharIsReady = false;            
            currentWord = ctrl.GetRandomWord();
            currentWord = currentWord.Trim();
            currentWord = currentWord.ToUpper();
            score = 0; //reset the score
            pointsPerCharacter = maxScore / currentWord.Length;
            ctrl.SetNextWord(currentWord); //write underscores to scrn
            currentCharIndex = 0;
            currentLetters = currentWord.ToCharArray();
            NextCharacter();
        }
      
        /// <summary>
        /// Finds and sets up the next character in the goal word.
        /// </summary>
        public void NextCharacter()
        {            
            ctrl.CharIsReady = false;

            //If no more characters, the word is complete
            if (currentCharIndex == currentWord.Length)
            {
                ctrl.WordCompleted();
            }

            //Otherwise, find and setup next character
            else
            {
                characterStartTime = DateTime.Now;
                currentChar = currentLetters[currentCharIndex];
                currentCharIndex++;
                ctrl.SetNextCharacter(currentChar);
                ctrl.CharIsReady = true;
            }
        }

        /// <summary>
        /// Updates the score upon completion of a character
        /// </summary>
        /// <param name="timeTaken">The time it took to complete the character</param>
        public void UpdateScore(TimeSpan timeTaken)
        {
            double scorableTime =  charGoalTime - timeTaken.Seconds - (timeTaken.Milliseconds / 1000);
            ctrl.WriteToDebugWindow(scorableTime + " secs of time to be scored");
            if (scorableTime < 0)
                scorableTime = 0;
            double characterScore = pointsPerCharacter * (scorableTime / charGoalTime);
            ctrl.WriteToDebugWindow("Charcter got " + characterScore + " / " + pointsPerCharacter);
            score = score + (int)characterScore;
        }        
    }
}
