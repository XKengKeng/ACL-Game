
#include <iostream>
#include <fstream>
#include <random>
#include <set>
#include <string>
using namespace std;

int main() {
    ofstream file("questions.csv");

    file << "ID,Category,Difficulty,Question,CorrectAnswer\n";

    mt19937 rng(random_device{}());
    uniform_int_distribution<int> coefficient(1, 20);
    uniform_int_distribution<int> solution(-20, 20);
    uniform_int_distribution<int> constant(-30, 30);

    set<string> used;

    int count = 0;

    while (count < 500) {
        int a = coefficient(rng);
        int x = solution(rng);
        int b = constant(rng);
        int c = a * x + b;                                                                                                                                      

        string question = to_string(a) + "x + (" +
            to_string(b) + ") = " + to_string(c);

        if (!used.insert(question).second)
            continue;

        string difficulty =
            a <= 5 ? "Easy" :
            a <= 12 ? "Medium" : "Hard";

        file << count + 1 << ",Linear Equation,"
             << difficulty << ","
             << question << "," << x << "\n";

        count++;
    }

    file.close();

    cout << "Successfully generated 500 questions!"
         << endl;

    return 0;
}
