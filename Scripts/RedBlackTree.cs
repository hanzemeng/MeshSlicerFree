using System.Collections.Generic;

// original code taken from https://github.com/AyazDyshin/Red-Black-Tree/tree/main

namespace Hanzzz.MeshSlicerFree
{
    using static RBTColor;
    enum RBTColor { R, B }
    class Node<T>
    {
        public T value;
        public RBTColor color;
        public Node<T> left;
        public Node<T> right;
        public Node<T> parent;
        public static Node<T> Null = new Node<T>(B);
        public Node(T value,RBTColor color,Node<T> left,Node<T> right,Node<T> parent)
        {
            this.value = value;
            this.color = color;
            this.left = left;
            this.right = right;
            this.parent = parent;
        }
        public Node(T value,Node<T> left,Node<T> right,Node<T> parent)
        {
            this.value = value;
            this.left = left;
            this.right = right;
            this.parent = parent;
        }
        public Node(RBTColor color)
        {
            this.color = color;
        }
    }

    class Tree<T>
    {
        public Node<T> root;
        public Comparer<T> comparer;
        public Tree(Comparer<T> comparer)
        {
            root = Node<T>.Null;
            this.comparer = comparer;
        }

        public void Clear()
        {
            root = Node<T>.Null;
        }

        public Node<T> GetMinNode()
        {
            if(Node<T>.Null == root)
            {
                return Node<T>.Null;
            }
            Node<T> res = root;
            while(res.left != Node<T>.Null)
            {
                res = res.left;
            }
            return res;
        }
        public Node<T> GetMaxNode()
        {
            if(Node<T>.Null == root)
            {
                return Node<T>.Null;
            }
            Node<T> res = root;
            while(res.right != Node<T>.Null)
            {
                res = res.right;
            }
            return res;
        }
        public Node<T> GetNodeLowerBound(T value)
        {
            int result;
            Node<T> res = Node<T>.Null;
            Node<T> treeNode = root;

            // traverse tree until node is found, record every left turn
            while (treeNode != Node<T>.Null)
            {
                result = comparer.Compare(value,treeNode.value);
                if(result < 0)
                {
                    res = treeNode;
                    treeNode = treeNode.left;
                }
                else if(result > 0)
                {
                    treeNode = treeNode.right;
                }
                else
                {
                    res = treeNode;
                    break;
                }
            }
            return res;
        }
        // returns the next node
        public Node<T> GetNextNode(Node<T> node)
        {
            if(Node<T>.Null != node.right)
            {
                node = node.right;
                while(Node<T>.Null != node.left)
                {
                    node = node.left;
                }
                return node;
            }

            while(Node<T>.Null != node.parent)
            {
                if(node.parent.left == node)
                {
                    return node.parent;
                }
                node = node.parent;
            }
            return Node<T>.Null;
        }

        // returns the previous node
        public Node<T> GetPreviousNode(Node<T> node)
        {
            if(Node<T>.Null != node.left)
            {
                node = node.left;
                while(Node<T>.Null != node.right)
                {
                    node = node.right;
                }
                return node;
            }

            while(Node<T>.Null != node.parent)
            {
                if(node.parent.right == node)
                {
                    return node.parent;
                }
                node = node.parent;
            }
            return Node<T>.Null;
        }

        public Node<T> GetNode(T val)
        {
            return GetNode(val, root);
        }
        public Node<T> GetNode(T val, Node<T> N)
        {
            if(N == Node<T>.Null)
            {
                return Node<T>.Null;
            }

            int res = comparer.Compare(N.value, val);
            if(res < 0)
            {
                return GetNode(val,N.right);
            }
            if(res > 0)
            {
                return GetNode(val,N.left);
            }
            return N;
        }
        public bool Contains(T val)
        {
            return Contains(val, root);
        }
        public bool Contains(T val, Node<T> N)
        {
            return Node<T>.Null != GetNode(val, N);
        }

        private void LeftRotate(Node<T> x)
        {
            var y = x.right;
            x.right = y.left;
            if(y.left != Node<T>.Null)
            {
                y.left.parent = x;
            }
            y.parent = x.parent;
            if(x.parent == Node<T>.Null)
            {
                root = y;
            }
            else if(x == x.parent.left)
            {
                x.parent.left = y;
            }
            else
            {
                x.parent.right = y;
            }

            y.left = x;
            x.parent = y;
        }
        private void RightRotate(Node<T> x)
        {
            var y = x.left;
            x.left = y.right;
            if(y.right != Node<T>.Null)
            {
                y.right.parent = x;
            }
            y.parent = x.parent;
            if(x.parent == Node<T>.Null)
            {
                root = y;
            }
            else if(x == x.parent.right)
            {
                x.parent.right = y;
            }
            else
            {
                x.parent.left = y;
            }

            y.right = x;
            x.parent = y;
        }
        private void InsertFixUp(Node<T> z)
        {
            while(z.parent.color == R)
            {
                if(z.parent == z.parent.parent.left)
                {
                    var y = z.parent.parent.right;
                    if(y.color == R)
                    {
                        z.parent.color = B;
                        y.color = B;
                        z.parent.parent.color = R;
                        z = z.parent.parent;
                    }
                    else
                    {
                        if(z == z.parent.right)
                        {
                            z = z.parent;
                            this.LeftRotate(z);
                        }
                        z.parent.color = B;
                        z.parent.parent.color = R;
                        this.RightRotate(z.parent.parent);
                    }
                }
                else
                {
                    var y = z.parent.parent.left;
                    if(y.color == R)
                    {
                        z.parent.color = B;
                        y.color = B;
                        z.parent.parent.color = R;
                        z = z.parent.parent;
                    }
                    else
                    {
                        if(z == z.parent.left)
                        {
                            z = z.parent;
                            this.RightRotate(z);
                        }
                        z.parent.color = B;
                        z.parent.parent.color = R;
                        this.LeftRotate(z.parent.parent);
                    }
                }
            }
            root.color = B;
        }

        public bool Insert(T f)
        {
            if(Contains(f,root))
            {
                return false;
            }
            else
            {
                Node<T> z = new Node<T>(f, Node<T>.Null, Node<T>.Null, Node<T>.Null);

                var y = Node<T>.Null;
                var x = root;
                while(x != Node<T>.Null)
                {
                    y = x;
                    if(comparer.Compare(z.value,x.value) < 0)
                    {
                        x = x.left;
                    }
                    else
                    {
                        x = x.right;
                    }
                }
                z.parent = y;
                if(y == Node<T>.Null)
                {
                    root = z;
                }
                else
                {
                    if(comparer.Compare(z.value,y.value) < 0)
                    {
                        y.left = z;
                    }
                    else
                    {
                        y.right = z;
                    }
                }

                z.left = Node<T>.Null;
                z.right = Node<T>.Null;
                z.color = R;
                this.InsertFixUp(z);
                return true;
            }
        }
        private Node<T> ContainsGiveMeNode(T val,Node<T> N)
        {
            if(N == Node<T>.Null)
            {
                return Node<T>.Null;
            }

            int res = comparer.Compare(N.value, val);
            if(res < 0)
            {
                return ContainsGiveMeNode(val,N.right);
            }
            if(res > 0)
            {
                return ContainsGiveMeNode(val,N.left);
            }
            return N;
        }
        private static Node<T> GiveMeDeleteReplacement(Node<T> t)
        {
            Node<T> temp = t.right;
            while(temp.left != Node<T>.Null)
            {
                temp = temp.left;
            }
            return temp;
        }
        public void DeleteFixUp(Node<T> x)
        {
            while((x != root) && (x.color == B))
            {
                if(x == x.parent.left)
                {
                    var w = x.parent.right;
                    if(w.color == R)
                    {
                        w.color = B;
                        x.parent.color = R;
                        this.LeftRotate(x.parent);
                        w = x.parent.right;
                    }
                    if((w.left.color == B) && (w.right.color == B))
                    {
                        w.color = R;
                        x = x.parent;
                    }
                    else
                    {
                        if(w.right.color == B)
                        {
                            w.left.color = B;
                            w.color = R;
                            this.RightRotate(w);
                            w = x.parent.right;
                        }
                        w.color = x.parent.color;
                        x.parent.color = B;
                        w.right.color = B;
                        this.LeftRotate(x.parent);
                        x = root;
                    }
                }
                else
                {
                    var w = x.parent.left;
                    if(w.color == R)
                    {
                        w.color = B;
                        x.parent.color = R;
                        this.RightRotate(x.parent);
                        w = x.parent.left;
                    }
                    if((w.right.color == B) && (w.left.color == B))
                    {
                        w.color = R;
                        x = x.parent;
                    }
                    else
                    {
                        if(w.left.color == B)
                        {
                            w.right.color = B;
                            w.color = R;
                            this.LeftRotate(w);
                            w = x.parent.left;
                        }
                        w.color = x.parent.color;
                        x.parent.color = B;
                        w.left.color = B;
                        this.RightRotate(x.parent);
                        x = root;
                    }
                }
            }
            x.color = B;
        }

        public bool Delete(T f)
        {
            if(!Contains(f,root))
            {
                return false;
            }

            Delete(ContainsGiveMeNode(f, root));

            return true;
        }

        public void Delete(Node<T> z)
        {
            Node<T> x;
            Node<T> y;
            
            if(z.left == Node<T>.Null || z.right == Node<T>.Null)
            {
                y = z;
            }
            else
            {
                y = GiveMeDeleteReplacement(z);
            }
            if(y.left != Node<T>.Null)
            {
                x = y.left;
            }
            else
            {
                x = y.right;
            }

            x.parent = y.parent;

            if(y.parent == Node<T>.Null)
            {
                root = x;
            }
            else
            {
                if(y == y.parent.left)
                {
                    y.parent.left = x;
                }
                else
                {
                    y.parent.right = x;
                }
            }
            if(y != z)
            {
                z.value = y.value;
            }
            if(y.color == B)
            {
                this.DeleteFixUp(x);
            }
        }

        private int BlackHeight(Node<T> n,int counter)
        {
            if(n == null)
            {
                return 1;
            }
            if(n.color == B)
            {
                counter++;

            }
            if((n.left == null) && (n.right == null))
            {
                return counter;
            }
            else if((n.left != null) && (n.right == null))
            {
                if(counter == BlackHeight(n.left,counter))
                {
                    return counter;
                }
                else
                {
                    return -1;
                }
            }
            else if((n.left == null) && (n.right != null))
            {
                if(counter == BlackHeight(n.right,counter))
                {
                    return counter;
                }
                else
                {
                    return -1;
                }
            }
            else if((n.left != null) && (n.right != null))
            {
                if(BlackHeight(n.left,counter) != BlackHeight(n.right,counter))
                {
                    return -1;
                }
                else
                {
                    return BlackHeight(n.left,counter);
                }
            }

            else
            {
                return -1;
            }

        }
        private bool RedCondition(Node<T> n)
        {
            if(n == Node<T>.Null)
            {
                return true;
            }
            if(n.color == R)
            {
                if((n.left.color == R) || (n.right.color == R))
                {
                    return false;
                }
            }
            if(n.left != Node<T>.Null)
            {
                return RedCondition(n.left);
            }
            if(n.right != Node<T>.Null)
            {
                return RedCondition(n.right);
            }
            return true;
        }
        private bool RootBlack(Tree<T> t)
        {
            if(t.root.color == B)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool Check()
        {
            if((BlackHeight(root,0) == -1) || !RedCondition(root) || !RootBlack(this))
            {
                return false;
            }

            Node<T> cur = GetMinNode();
            if(Node<T>.Null == cur)
            {
                return true;
            }

            Node<T> next = GetNextNode(cur);
            while(Node<T>.Null != next)
            {
                if(comparer.Compare(cur.value,next.value) > 0)
                {
                    return false;
                }
                cur = next;
                next = GetNextNode(next);
            }
            return true;
        }
    }
}
