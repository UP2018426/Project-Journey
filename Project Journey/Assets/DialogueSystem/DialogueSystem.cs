using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class DialogueSystem : MonoBehaviour
{
    TextMeshPro tmp;
    [SerializeField] string userText;

    const string specialCharacters = "!£$%&@?";

    // Start is called before the first frame update
    void Start()
    {
        tmp = GetComponent<TextMeshPro>();
        tmp.text = userText;
    }

    // Update is called once per frame
    void Update()
    {   
        if(Input.GetKeyUp(KeyCode.R))
        {
            string[] words = SplitSentenceToWords(userText);
            
            List<string> shuffledWords = new List<string>();

            foreach(string word in words)
            {
                switch (UnityEngine.Random.Range(0, 1))
                {
                    case 0 :
                        shuffledWords.Add(ShuffleString(word));
                        break;
                    case 1 :
                        shuffledWords.Add(WordToSpecialCharacters(word));
                        break;
                    default:
                        break;
                }

                //shuffledWords.Add(ShuffleString(word));
                //shuffledWords.Add(WordToSpecialCharacters(word));
            }

            tmp.text = CombineWordsToSentence(shuffledWords);
        }
    }

    string ShuffleString(string inWord)
    {
        char[] charArray = inWord.ToCharArray();

        bool bContainsPeriod = charArray.Contains('.');
        bool bContainsComma = charArray.Contains(',');

        if(bContainsPeriod)
        {
            List<char> charList = charArray.ToList();

            for (int i = 0; i < charArray.Length; i++)
            {
                if (charArray[i] == '.')
                {
                    charList.RemoveAt(i);
                }
            }

            charArray = charList.ToArray();
        }

        if (bContainsComma)
        {
            List<char> charList = charArray.ToList();

            for (int i = 0; i < charArray.Length; i++)
            {
                if (charArray[i] == ',')
                {
                    charList.RemoveAt(i);
                }
            }

            charArray = charList.ToArray();
        }

        int n = charArray.Length;
        while (n > 1)
        {
            System.Random randomNumber = new System.Random();
            n--;
            int k = randomNumber.Next(n + 1);
            var value = charArray[k];
            charArray[k] = charArray[n];
            charArray[n] = value;
        }

        string returnString = new string(charArray);

        if (bContainsPeriod)
        {
            returnString += ". ";
        }
        if (bContainsComma)
        {
            returnString += ", ";
        }
        else
        {
            returnString += ' ';
        }

        return returnString;
    }

    string[] SplitSentenceToWords(string sentence)
    {
        string[] arrayOfWords = sentence.Split(' ');

        return arrayOfWords;
    }

    string CombineWordsToSentence(List<string> inList)
    {
        string returnString = null;

        foreach (string word in inList)
        {
            returnString += word;
        }

        return returnString;
    }

    string WordToSpecialCharacters(string inWord)
    {
        string returnString = "";

        char[] charArray = inWord.ToCharArray();

        bool bContainsPeriod = charArray.Contains('.');
        bool bContainsComma = charArray.Contains(',');

        if (bContainsPeriod)
        {
            List<char> charList = charArray.ToList();

            for (int i = 0; i < charArray.Length; i++)
            {
                if (charArray[i] == '.')
                {
                    charList.RemoveAt(i);
                }
            }

            charArray = charList.ToArray();
        }

        if (bContainsComma)
        {
            List<char> charList = charArray.ToList();

            for (int i = 0; i < charArray.Length; i++)
            {
                if (charArray[i] == ',')
                {
                    charList.RemoveAt(i);
                }
            }

            charArray = charList.ToArray();
        }

        for (int i = 0; i < inWord.Length-1; i++)
        {
            int charToChoose = UnityEngine.Random.Range(0, specialCharacters.Length);

            returnString += specialCharacters[charToChoose];
        }

        if (bContainsPeriod)
        {
            returnString += ". ";
        }
        if (bContainsComma)
        {
            returnString += ", ";
        }
        else
        {
            returnString += ' ';
        }

        return returnString;
    }
}
