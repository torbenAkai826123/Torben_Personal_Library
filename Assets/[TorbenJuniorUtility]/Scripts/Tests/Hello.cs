using UnityEngine;
namespace Torben.Tests
{
    [System.Serializable]
    public class Hello : MonoBehaviour
    {

        [SerializeField]
        int number = 0;

        int _number = 0;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            print("Hello World! " + number);


        }

        // Update is called once per frame
        void Update()
        {
            if (number != _number)
            {
                print("Hello World! " + number);
                _number = number;
            }

        }

    }
}
