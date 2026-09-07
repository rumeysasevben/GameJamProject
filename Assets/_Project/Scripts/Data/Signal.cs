using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single symbol in a signal. Short is a tap, Long is a held pulse.
/// </summary>
public enum SignalSymbol
{
    Short,
    Long
}

/// <summary>
/// An ordered sequence of <see cref="SignalSymbol"/>s. Ships emit one and the
/// player echoes it on the bar; the two are compared for equality to decide
/// whether the ship binds to the beam.
/// Plain data type — deliberately not a MonoBehaviour, but marked Serializable
/// so it can be filled in on a ship in the Inspector.
/// </summary>
[Serializable]
public class Signal : IEquatable<Signal>
{
    [Tooltip("The symbols in order, as the ship flashes them.")]
    [SerializeField] private List<SignalSymbol> symbols = new List<SignalSymbol>();

    /// <summary>
    /// The symbols in order. Never null: an empty list means an empty signal.
    /// </summary>
    public List<SignalSymbol> Symbols
    {
        get
        {
            if (symbols == null)
            {
                symbols = new List<SignalSymbol>();
            }

            return symbols;
        }
    }

    /// <summary>Number of symbols in the sequence.</summary>
    public int Count => Symbols.Count;

    /// <summary>Creates an empty signal.</summary>
    public Signal()
    {
    }

    /// <summary>
    /// Creates a signal from an existing sequence. The symbols are copied, so
    /// later changes to <paramref name="source"/> do not affect this signal.
    /// A null argument is treated as an empty sequence.
    /// </summary>
    public Signal(IEnumerable<SignalSymbol> source)
    {
        symbols = source == null
            ? new List<SignalSymbol>()
            : new List<SignalSymbol>(source);
    }

    /// <summary>Appends a symbol to the end of the sequence.</summary>
    public void Add(SignalSymbol symbol)
    {
        Symbols.Add(symbol);
    }

    /// <summary>Removes every symbol, leaving an empty signal.</summary>
    public void Clear()
    {
        Symbols.Clear();
    }

    /// <summary>
    /// True when both signals hold the same symbols in the same order.
    /// Two empty signals are equal; null is never equal to a signal.
    /// </summary>
    public bool Equals(Signal other)
    {
        if (ReferenceEquals(other, null))
        {
            return false;
        }

        if (ReferenceEquals(other, this))
        {
            return true;
        }

        if (Symbols.Count != other.Symbols.Count)
        {
            return false;
        }

        for (int i = 0; i < Symbols.Count; i++)
        {
            if (Symbols[i] != other.Symbols[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return Equals(obj as Signal);
    }

    /// <summary>
    /// Order-sensitive hash over the symbols, so Short-Long and Long-Short do
    /// not collide. Only valid while the signal is unmodified — see the note
    /// on mutability in the class remarks.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < Symbols.Count; i++)
            {
                hash = hash * 31 + (int)Symbols[i];
            }

            return hash;
        }
    }

    /// <summary>Compact debug form, e.g. "Signal(..-.)" — '.' is Short, '-' is Long.</summary>
    public override string ToString()
    {
        char[] glyphs = new char[Symbols.Count];
        for (int i = 0; i < Symbols.Count; i++)
        {
            glyphs[i] = Symbols[i] == SignalSymbol.Short ? '.' : '-';
        }

        return $"Signal({new string(glyphs)})";
    }
}
