// Edytor kodu C# dla aplikacji GamePlayer.
// Komunikacja z C#:
//   JS -> C#: window.Android.loadCode(), window.Android.compile(code)
//   C# -> JS: window.onCompileResult(json)
import { EditorView, basicSetup } from "codemirror";
import { EditorState } from "@codemirror/state";
import { keymap } from "@codemirror/view";
import { indentWithTab } from "@codemirror/commands";
import { StreamLanguage, indentUnit } from "@codemirror/language";
import { csharp } from "@codemirror/legacy-modes/mode/clike";
import { lintGutter, setDiagnostics } from "@codemirror/lint";
import { oneDark } from "@codemirror/theme-one-dark";

const bridge = window.Android;
const status = document.getElementById("status");
const playButton = document.getElementById("play");

const view = new EditorView({
  parent: document.getElementById("editor"),
  state: EditorState.create({
    doc: bridge ? bridge.loadCode() : "// brak mostka Android\n",
    extensions: [
      basicSetup,
      keymap.of([indentWithTab]),
      indentUnit.of("    "),
      StreamLanguage.define(csharp),
      lintGutter(),
      oneDark,
      EditorView.theme({ "&": { height: "100%" }, ".cm-scroller": { fontSize: "14px" } }),
    ],
  }),
});

function setStatus(text, kind) {
  status.textContent = text;
  status.className = kind || "";
}

playButton.addEventListener("click", () => {
  if (!bridge) return;
  playButton.disabled = true;
  setStatus("Kompiluję…");
  view.dispatch(setDiagnostics(view.state, []));
  // Oddajemy klatkę przeglądarce, żeby status zdążył się odświeżyć.
  setTimeout(() => bridge.compile(view.state.doc.toString()), 16);
});

window.onCompileResult = (result) => {
  playButton.disabled = false;
  const length = view.state.doc.length;
  const diagnostics = result.diagnostics.map((d) => ({
    from: Math.min(d.from, length),
    to: Math.min(Math.max(d.to, d.from), length),
    severity: d.severity,
    message: d.message,
  }));
  view.dispatch(setDiagnostics(view.state, diagnostics));

  const errors = result.diagnostics.filter((d) => d.severity === "error");
  if (result.ok) {
    setStatus(`OK (${result.ms} ms)`, "ok");
  } else if (errors.length) {
    const first = errors[0];
    setStatus(`${errors.length} bł. — linia ${first.line}: ${first.message}`, "error");
    view.dispatch({ selection: { anchor: Math.min(first.from, length) }, scrollIntoView: true });
  } else {
    setStatus(result.message || "Błąd", "error");
  }
};

// Pasek symboli, których brakuje na klawiaturze telefonu.
for (const button of document.querySelectorAll("#symbols button")) {
  button.addEventListener("mousedown", (e) => e.preventDefault()); // nie zabieraj fokusu edytorowi
  button.addEventListener("click", () => {
    const text = button.dataset.insert ?? button.textContent;
    view.dispatch(view.state.replaceSelection(text));
    view.focus();
  });
}
