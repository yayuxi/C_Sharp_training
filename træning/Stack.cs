namespace træning;

public class Stack
{
    private List<int> stackList = new List<int>();

    public void Push(int value) {
        stackList.Add(value);
    }

    public int Pop() {
        if (IsEmpty()) {
            throw new InvalidOperationException("Stack is empty");
        }
        int value = stackList[stackList.Count - 1];
        stackList.RemoveAt(stackList.Count - 1);
        return value;
    }

    public int Peek() {
        if (stackList.Count == 0) {
            throw new InvalidOperationException("Stack is empty");
        }
        return stackList[stackList.Count - 1];
    }

    public bool IsEmpty() {
        return stackList.Count == 0;
    }
}