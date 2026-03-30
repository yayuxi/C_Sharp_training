namespace træning;

public class Stack<T>
{
    private List<T> stackList;
    
    public Stack() {
        stackList = new List<T>();
    }

    public void Push(T item) {
        stackList.Add(item);
    }

    public T Pop() {
        if (IsEmpty()) {
            throw new InvalidOperationException("Stack is empty");
        }
        T item = stackList[stackList.Count - 1];
        stackList.RemoveAt(stackList.Count - 1);
        return item;
    }

    public T Peek() {
        if (stackList.Count == 0) {
            throw new InvalidOperationException("Stack is empty");
        }
        return stackList[stackList.Count - 1];
    }

    public bool IsEmpty() {
        return stackList.Count == 0;
    }
}