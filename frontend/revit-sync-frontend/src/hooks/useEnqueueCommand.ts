import { useMutation } from "@tanstack/react-query";

export type AddBoxesCommand = {
  projectName: string;
  type: "ADD_BOXES";
  boxes: Array<{
    category?: string;
    centerX: number;
    centerY: number;
    centerZ: number;
    sizeX: number;
    sizeY: number;
    sizeZ: number;
  }>;
};

export type DeleteElementsCommand = {
  projectName: string;
  type: "DELETE_ELEMENTS";
  elementIds: string[];
};

export type MoveElementCommand = {
  projectName: string;
  type: "MOVE_ELEMENT";
  targetElementId: string;
  newCenterX: number;
  newCenterY: number;
  newCenterZ: number;
};

export type GeometryCommand = AddBoxesCommand | DeleteElementsCommand | MoveElementCommand;

export function useEnqueueCommand() {
  return useMutation({
    mutationFn: async (cmd: GeometryCommand) => {
      const r = await fetch("http://localhost:5245/api/commands", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(cmd),
      });
      if (!r.ok) throw new Error(await r.text());
      return r.json() as Promise<{ commandId: string }>;
    },
  });
}
